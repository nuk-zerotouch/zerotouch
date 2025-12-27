import asyncio
import websockets
import json
import time
import random
import pyautogui 

# Global state
clients = set()

gesture_debug = False
driver_debug = False

# Utils
async def broadcast(message: dict, exclude=None):
    if not clients:
        return
    data = json.dumps(message)
    targets = [c for c in clients if c != exclude] # Exclude the sender if specified
    if targets:
        await asyncio.gather(*(c.send(data) for c in targets), return_exceptions=True)

GESTURES = [
    "swipe_left",
    "swipe_right",
    "push",
    "tap",
    "rotate_clockwise",
    "rotate_counterclockwise"
]
GESTURE_KEY_MAP = {
    "swipe_left": "left",   
    "swipe_right": "right",  
    "push": "enter",         
    "tap": "space",          
    "rotate_clockwise": "down",         
    "rotate_counterclockwise": "up"     
}

async def handle_client(ws):
    global gesture_debug, driver_debug
    clients.add(ws)
    print(f"Client connected. Total: {len(clients)}")

    try:
        async for msg in ws:
            try:
                data = json.loads(msg)
                # print("[RECV]", data) # 除錯用

                if data.get("type") == "gesture":
                    gesture_name = data.get("gesture")
                    
                    # 鍵盤模擬 (Debug 用，若會重複觸發請註解掉)
                    if gesture_name in GESTURE_KEY_MAP:
                        key_to_press = GESTURE_KEY_MAP[gesture_name]
                        print(f"[KEYBOARD] {gesture_name} -> {key_to_press}")
                        pyautogui.press(key_to_press)
                    
                    # 廣播給前端 (讓 MainWindowViewModel 處理間接連動)
                    await broadcast(data, exclude=ws)

                elif data.get("type") == "command":
                    # ... 處理指令邏輯 ...
                    pass

            except json.JSONDecodeError:
                print("[ERROR] Received invalid JSON data")
            except Exception as e:
                print(f"[ERROR] Process error: {e}")
    
    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        clients.remove(ws)
        print("Client disconnected.")
async def gesture_debug_loop():
    while True:
        await asyncio.sleep(random.uniform(0.5, 2.0))

        if not gesture_debug:
            continue

        event = {
            "type": "gesture",
            "ts": round(time.time(), 3),
            "gesture": random.choice(GESTURES),
            "confidence": round(random.uniform(0.8, 1.0), 2),
            "source": "debug"
        }

        print("[SEND][GESTURE]", event)
        await broadcast(event)

async def driver_debug_loop():
    last_sent = 0
    COOLDOWN = 20

    while True:
        await asyncio.sleep(1.0)

        if not driver_debug:
            continue

        now = time.time()
        if now - last_sent < COOLDOWN:
            continue

        event = {
            "type": "driver_state",
            "fatigue": True,
            "yawn": False,
            "eye_closed": True,
            "confidence": 0.95,
            "source": "debug"
        }

        last_sent = now
        print("[SEND][DRIVER]", event)
        await broadcast(event)

async def main():
    print("WebSocket server running on ws://0.0.0.0:8765")

    server = await websockets.serve(handle_client, "0.0.0.0", 8765)

    await asyncio.gather(
        server.wait_closed(),
        gesture_debug_loop(),
        driver_debug_loop()
    )


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("Server stopped manually.")
    