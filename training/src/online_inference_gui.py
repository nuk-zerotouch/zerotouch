# realtime_infer_with_gui.py
import asyncio
import os
import queue
import sys
import threading
import time
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
from PySide2 import QtWidgets
from typing import Dict, Tuple
import websockets
import json

# --- WebSocket Config ---
WS_URL = "ws://127.0.0.1:8765"
gesture_queue = queue.Queue()

# ======== 路徑/參數 ========
MODEL_PATH   = r"C:\Users\72161\OneDrive\文件\GitHub\k60168a-dongle-utilities\radar-gesture-recognition\src\output\left2rightnew.pth"
SETTING_FILE = r"C:\Users\72161\Downloads\Collect_RDI\Collect_RDI\TempParam\K60168-Test-00256-008-v0.0.8-20230717_60cm"
WINDOW_SIZE  = 30                    # 滑動視窗幀數
CLASS_NAMES = ['background', 'left2right', 'right2left', 'up2down', 'down2up', 'push', 'circle']
ENTER_TH = 0.30  # 拉高到 0.85 甚至 0.90，只有信心爆棚時才承認是手勢
EXIT_TH  = 0.20  # 退出門檻設為 0.4，避免手勢快結束時的邊緣跳動
STREAM_TYPE  = "feature_map"         # 或 "raw_data"
NUM_CLASSES  = len(CLASS_NAMES)
# ======================================

# 需提供 gesture_gui_pyside.py，且類別有 update_probabilities(bg, pat, wave, come, current)
from gesture_gui_pyside import GestureGUI

# ======== Kaiku / KKT imports ========
from KKT_Module import kgl
from KKT_Module.DataReceive.Core import Results
from KKT_Module.DataReceive.DataReceiver import MultiResult4168BReceiver
from KKT_Module.FiniteReceiverMachine import FRM
from KKT_Module.SettingProcess.SettingConfig import SettingConfigs
from KKT_Module.SettingProcess.SettingProccess import SettingProc
from KKT_Module.GuiUpdater.GuiUpdater import Updater

def websocket_worker():
    """ 獨立執行緒，負責維持連線並發送手勢 """
    async def send_loop():
        while True:
            try:
                # 建立連線
                async with websockets.connect(WS_URL) as websocket:
                    print(f"[WS] Connected to {WS_URL}")
                    
                    while True:
                        try:
                            # 如果 Queue 是空的，它會立刻丟出 queue.Empty 異常，不會卡住
                            gesture_data = gesture_queue.get_nowait()
                            
                            # 有資料就送出
                            await websocket.send(json.dumps(gesture_data))
                            
                        except queue.Empty:
                            # Queue 空的時候，休息 0.1 秒
                            # 讓出控制權給 Asyncio 核心去處理 Ping/Pong 心跳
                            await asyncio.sleep(0.1)
                            
            except Exception as e:
                print(f"[WS] Connection error: {e}. Retrying in 3s...")
                await asyncio.sleep(3)

    # 啟動 Async Event Loop
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    loop.run_until_complete(send_loop())
    loop.close()

# 啟動 Worker
ws_thread = threading.Thread(target=websocket_worker, daemon=True)
ws_thread.start()

# ---------- Kaiku helpers ----------
def connect_device():
    try:
        device = kgl.ksoclib.connectDevice()
        if device == 'Unknow':
            ret = QtWidgets.QMessageBox.warning(
                None, 'Unknown Device', 'Please reconnect device and try again',
                QtWidgets.QMessageBox.Ok | QtWidgets.QMessageBox.Cancel
            )
            if ret == QtWidgets.QMessageBox.Ok:
                connect_device()
    except Exception:
        ret = QtWidgets.QMessageBox.warning(
            None, 'Connection Failed', 'Please reconnect device and try again',
            QtWidgets.QMessageBox.Ok | QtWidgets.QMessageBox.Cancel
        )
        if ret == QtWidgets.QMessageBox.Ok:
            connect_device()

def run_setting_script(setting_name: str):
    ksp = SettingProc()
    cfg = SettingConfigs()
    cfg.Chip_ID = kgl.ksoclib.getChipID().split(' ')[0]
    cfg.Processes = [
        'Reset Device',
        'Gen Process Script',
        'Gen Param Dict', 'Get Gesture Dict',
        'Set Script',
        'Run SIC',
        'Phase Calibration',
        'Modulation On'
    ]
    cfg.setScriptDir(f'{setting_name}')
    ksp.startUp(cfg)

def set_properties(obj: object, **kwargs):
    print(f"==== Set properties in {obj.__class__.__name__} ====")
    for k, v in kwargs.items():
        if not hasattr(obj, k):
            print(f'Attribute "{k}" not in {obj.__class__.__name__}.')
            continue
        setattr(obj, k, v)
        print(f'Attribute "{k}", set "{v}"')

# ---------- 3D CNN ----------
class Gesture2DCNN_LSTM(nn.Module):
    def __init__(self, num_classes = NUM_CLASSES):
        super(Gesture2DCNN_LSTM, self).__init__()
        self.features = nn.Sequential(
            nn.Conv2d(in_channels=2, out_channels=8, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(kernel_size=2),
            
            nn.Conv2d(8, 16, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(kernel_size=2),
            
            nn.Conv2d(16, 32, kernel_size=3, padding=1),
            nn.ReLU(),
            nn.MaxPool2d(kernel_size=2),

            nn.Flatten(),
            nn.Linear(32*4*4, 128),
            nn.BatchNorm1d(128),
            nn.ReLU(),
            nn.Dropout(0.5)

        )
        self.lstm = nn.LSTM(input_size=128, hidden_size=128, num_layers=1, batch_first=True)
        self.classifier = nn.Linear(128, num_classes)
    
    def forward(self, x):
        B, C, T, H, W = x.shape
        x = x.permute(0, 2, 1, 3, 4)   # (B, T, C, H, W)
        cnn_in = x.reshape(B*T, C, H, W)        # CNN 處理每一幀

        cnn_out = self.features(cnn_in)

        lstm_in = cnn_out.reshape(B, T, -1)      # (B, T, feature_dim)

        lstm_out, _ = self.lstm(lstm_in)
        last_out = lstm_out[:, -1, :]

        out = self.classifier(last_out)
        return out


def _maybe_remap_keys_to_classifier(state: dict) -> dict:
    # 若權重鍵名是 fc.*，轉成 classifier.*
    if any(k.startswith("fc.") for k in state.keys()):
        new = {}
        for k, v in state.items():
            new["classifier." + k[3:]] = v if k.startswith("fc.") else v
        return new
    return state

# ---------- 即時推論核心 ----------
class OnlineInferenceContext:
    def __init__(self, model: nn.Module, device: torch.device, window_size: int):
        self.model = model
        self.device = device
        self.window = window_size
        self.buffer = np.zeros((2, 32, 32, self.window), dtype=np.float32)
        self.collected = 0
        self.last_pred = CLASS_NAMES[0] # "background"
        self.last_trigger_time = 0
        self.cooldown_seconds = 1.0

    GESTURE_MAPPING = {
        "left2right": "swipe_right", # 對應 Key.Right
        "right2left": "swipe_left",  # 對應 Key.Left
        "push":       "push",        # 對應 Key.Enter
        "up2down":    "swipe_down",  # 對應 Key.Down
        "down2up":    "swipe_up",    # 對應 Key.Up
        "circle":     "rotate_clockwise" # Optional
    }

    @staticmethod
    def to_frame(arr) -> np.ndarray:
        x = np.asarray(arr)
        # 常見兩種： (2,32,32) 或 (32,32,2)
        if x.shape == (2, 32, 32):
            pass
        elif x.shape == (32, 32, 2):
            x = np.transpose(x, (2, 0, 1))
        else:
            raise ValueError(f"Unexpected frame shape: {x.shape}")
        
        # Channel 0: RDI (強度圖) -> Log 壓縮
        x[0, :, :] = np.log10(x[0, :, :] + 1)
        
        # Channel 1: Phase (相位圖) -> 除以 PI
        # 原本是 -PI~PI，現在變 -1~1
        x[1, :, :] = x[1, :, :] / np.pi

        return x
    def reset_buffer(self):
        """ 清空緩衝區，避免同一個動作產生的剩餘數據觸發二次偵測 """
        self.buffer = np.zeros((2, 32, 32, self.window), dtype=np.float32)
        self.collected = 0

    def push_and_infer(self, frame: np.ndarray):
        self.buffer = np.roll(self.buffer, shift=-1, axis=-1)
        self.buffer[..., -1] = frame
        self.collected += 1
        
        if self.collected < self.window:
            return None

        win = np.expand_dims(self.buffer, axis=0)
        win = np.transpose(win, (0, 1, 4, 2, 3))
        x = torch.from_numpy(win).float().to(self.device)

        with torch.no_grad():
            logits = self.model(x)
            p = F.softmax(logits, dim=1).cpu().numpy()[0]
        return p

    def apply_double_threshold(self, probs: np.ndarray) -> Tuple[str, bool, Dict[str, float]]:
        probs_dict = dict(zip(CLASS_NAMES, probs.tolist()))
        
        nonbg_probs = probs[1:]
        nonbg_names = CLASS_NAMES[1:]
        
        top_idx = int(np.argmax(nonbg_probs)) 
        top_name = nonbg_names[top_idx]
        top_prob = float(nonbg_probs[top_idx])
        
        current = CLASS_NAMES[0] # 預設都是 Background
        changed = False

        import time
        now = time.time()

        if top_prob >= ENTER_TH and (now - self.last_trigger_time > self.cooldown_seconds):
            
            current = top_name
            changed = True
            self.last_trigger_time = now
            
            print(f">>> [DETECTED] {top_name} (Prob: {top_prob:.2f}) - Context Reset.")

            # =========== 發送 WebSocket 訊號 ===========
            mapped_gesture = self.GESTURE_MAPPING.get(top_name)
            if mapped_gesture:
                event = {
                    "type": "gesture",
                    "gesture": mapped_gesture,
                    "confidence": float(top_prob),
                    "source": "radar"
                }
                gesture_queue.put(event)  
            # ===============================================

            self.reset_buffer() 
        else:
            current = CLASS_NAMES[0]

        return current, changed, probs_dict

# ----------  更新 GUI ----------
class InferenceUpdater(Updater):
    def __init__(self, ctx: OnlineInferenceContext, gesture_gui: GestureGUI, stream: str = "feature_map"):
        super().__init__()
        self.ctx = ctx
        self.gui = gesture_gui
        self.stream = stream

    def update(self, res: Results):
        try:
            if self.stream == "raw_data":
                arr = res['raw_data'].data
            else:
                arr = res['feature_map'].data


            frame = self.ctx.to_frame(arr)                 # (2,32,32) float32
            probs = self.ctx.push_and_infer(frame)         # None 或 (4,)
            if probs is None:
                return

            current, changed, probs_dict = self.ctx.apply_double_threshold(probs)

            # --- 更新 GUI ---
            try:
                self.gui.update_probabilities(probs_dict)
            except Exception:
                pass

            # --- 狀態變更時列印 ---
            if changed:
                # 打印所有 7 個手勢的機率
                log_str = f"[Pred] {current} | "
                for name, prob in probs_dict.items():
                    log_str += f"{name.upper()[:4]}:{prob:.2f} "
                print(log_str)

        except Exception:
            # 靜默跳過異常幀，避免卡住接收
            pass

# ---------- 主流程 ----------
def main():
    # 0) Qt 事件圈
    app = QtWidgets.QApplication(sys.argv)

    try:
        gui = GestureGUI()
        gui.show()

        print("--> Connecting to Device...")
        kgl.setLib()
        connect_device()
        
        print(f"--> Loading Settings from: {SETTING_FILE}")
        if not os.path.exists(SETTING_FILE):
             raise FileNotFoundError(f"找不到設定檔路徑: {SETTING_FILE}")
        run_setting_script(SETTING_FILE)

        if STREAM_TYPE == "raw_data":
            kgl.ksoclib.writeReg(0, 0x50000504, 5, 5, 0)
        else:
            kgl.ksoclib.writeReg(1, 0x50000504, 5, 5, 0)

        print(f"--> Loading Model from: {MODEL_PATH}")
        if not os.path.exists(MODEL_PATH):
             raise FileNotFoundError(f"找不到模型路徑: {MODEL_PATH}")

        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        model = Gesture2DCNN_LSTM(num_classes=NUM_CLASSES).to(device)
        state = torch.load(MODEL_PATH, map_location=device)
        if isinstance(state, dict) and "state_dict" in state:
            state = state["state_dict"]
        state = _maybe_remap_keys_to_classifier(state)
        model.load_state_dict(state, strict=False)
        model.eval()
        print(f"[INFO] model loaded successfully on {device}")

        ctx = OnlineInferenceContext(model=model, device=device, window_size=WINDOW_SIZE)
        updater = InferenceUpdater(ctx, gesture_gui=gui, stream=STREAM_TYPE)

        receiver = MultiResult4168BReceiver()
        set_properties(receiver,
                            actions=1,
                            rbank_ch_enable=7,
                            read_interrupt=0,
                            clear_interrupt=0)
        FRM.setReceiver(receiver)
        FRM.setUpdater(updater)
        
        print("--> Starting FRM...")
        FRM.trigger()
        FRM.start()

        print("[INFO] System Running. Press Ctrl+C to quit.")
        
        # 進入 Qt 主迴圈
        sys.exit(app.exec_())

    except Exception as e:
        # 防止視窗直接關閉
        import traceback
        traceback.print_exc()
        print("\n[ERROR] 程式發生錯誤，請檢查上方錯誤訊息。")
        input("請按 Enter 鍵離開...")  # 讓視窗停住

    except KeyboardInterrupt:
        pass
    finally:
        print("Cleaning up...")
        try:
            FRM.stop()
        except: pass
        try:
            kgl.ksoclib.closeDevice()
        except: pass
        print("[INFO] Stopped.")

if __name__ == "__main__":
    main()
