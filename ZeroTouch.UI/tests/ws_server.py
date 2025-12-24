import asyncio
import websockets
import json
import time
import random

# Global state
clients = set()

gesture_debug = False
driver_debug = False

# Utils
async def broadcast(message: dict):
    if not clients:
        return

    data = json.dumps(message)
    await asyncio.gather(*(c.send(data) for c in clients))

GESTURES = [
    "swipe_left",
    "swipe_right",
    "push",
    "tap",
    "rotate_clockwise",
    "rotate_counterclockwise"
]

async def handle_client(ws):
    global gesture_debug, driver_debug
    
    clients.add(ws)
    print("Client connected.")

    try:
        async for msg in ws:
            data = json.loads(msg)
            print("[RECV]", data)
    
            if data.get("type") == "command":
                cmd = data.get("cmd")
                
                if cmd == "toggle_gesture_debug":
                    gesture_debug = not gesture_debug
                    print("Gesture debug:", gesture_debug)
                    
                elif cmd == "toggle_driver_debug":
                    driver_debug = not driver_debug
                    print("Driver debug:", driver_debug)
    
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
    