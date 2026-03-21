import numpy as np
import matplotlib.pyplot as plt

# ======== 設定 ========
# 請換成你的訓練資料路徑
DATA_PATH = r"C:\Users\72161\OneDrive\文件\GitHub\k60168a-dongle-utilities\radar-gesture-recognition\src\data\processed_data\validation_dataset.npz"

# 你的類別名稱順序 (必須跟訓練時一樣)
CLASS_NAMES = ['background', 'left2right', 'right2left', 'up2down', 'down2up', 'push', 'circle']
L2R_IDX = 1  # Left2Right 的索引
R2L_IDX = 2  # Right2Left 的索引
# =====================

def check_data():
    print(f"正在載入: {DATA_PATH} ...")
    data = np.load(DATA_PATH)
    features = data['features']       # Shape 預期: (N, 2, 32, 32, 30)
    ground_truths = data['ground_truths']
    
    print(f"資料形狀: {features.shape}")
    
    # 1. 檢查 Channel 定義 & 數值範圍
    # 隨機抽樣 1000 個點來算就好
    sample_data = features[:1000] 
    ch0 = sample_data[:, 0, :, :, :]
    ch1 = sample_data[:, 1, :, :, :]
    
    print("\n=== [1] Channel 健康檢查 ===")
    print(f"Ch0 (預期是 RDI 能量): Min={np.min(ch0):.2f}, Max={np.max(ch0):.2f}, Mean={np.mean(ch0):.2f}")
    print(f"Ch1 (預期是 Phase 相位): Min={np.min(ch1):.2f}, Max={np.max(ch1):.2f}, Mean={np.mean(ch1):.2f}")
    
    if np.min(ch0) < 0:
        print("⚠️ 警告: Ch0 含有負數！這不像是 RDI 能量圖。請確認 Channel 順序。")
    if np.min(ch1) >= 0:
        print("⚠️ 警告: Ch1 全部都是正數！這不像是相位圖 (應該要有負值)。")

    # 2. 視覺化 L2R vs R2L 的相位差異
    print("\n=== [2] Left2Right vs Right2Left 相位對比 ===")
    
    l2r_clips = []
    r2l_clips = []
    
    # 篩選資料
    for i in range(len(features)):
        # 取中間那一幀的 label
        mid_label = np.argmax(ground_truths[i, 15]) 
        if mid_label == L2R_IDX:
            l2r_clips.append(features[i])
        elif mid_label == R2L_IDX:
            r2l_clips.append(features[i])
            
    l2r_data = np.array(l2r_clips)
    r2l_data = np.array(r2l_clips)
    
    print(f"找到 L2R 樣本數: {len(l2r_data)}")
    print(f"找到 R2L 樣本數: {len(r2l_data)}")
    
    if len(l2r_data) == 0 or len(r2l_data) == 0:
        print("❌ 樣本不足，無法畫圖")
        return

    # 計算平均相位圖 (Channel 1)
    # 沿著 Sample(0) 和 Time(-1) 軸做平均 -> 得到 (32, 32) 的圖
    l2r_phase_avg = np.mean(np.mean(l2r_data[:, 1, :, :, :], axis=-1), axis=0)
    r2l_phase_avg = np.mean(np.mean(r2l_data[:, 1, :, :, :], axis=-1), axis=0)
    
    # 畫圖
    plt.figure(figsize=(12, 5))
    
    # 設定顏色範圍 (讓紅藍對稱)
    limit = max(np.max(np.abs(l2r_phase_avg)), np.max(np.abs(r2l_phase_avg)))
    
    plt.subplot(1, 2, 1)
    plt.imshow(l2r_phase_avg, cmap='seismic', vmin=-limit, vmax=limit)
    plt.colorbar()
    plt.title(f"Left2Right Average Phase\n(Count: {len(l2r_data)})")
    
    plt.subplot(1, 2, 2)
    plt.imshow(r2l_phase_avg, cmap='seismic', vmin=-limit, vmax=limit)
    plt.colorbar()
    plt.title(f"Right2Left Average Phase\n(Count: {len(r2l_data)})")
    
    plt.suptitle("Check Colors: They should be OPPOSITE (e.g., Red vs Blue)")
    plt.show()

if __name__ == "__main__":
    check_data()