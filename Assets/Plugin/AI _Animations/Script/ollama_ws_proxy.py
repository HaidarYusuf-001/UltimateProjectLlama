import asyncio
import json
import websockets
import aiohttp

OLLAMA_URL = "http://127.0.0.1:11434/api/generate"

async def handle_ws(websocket):
    async for message in websocket:
        prompt = message
        print(f"Received prompt: {prompt}")

        async with aiohttp.ClientSession() as session:
            headers = {"Content-Type": "application/json"}
            payload = {
                "model": "umm-informatics-knowledgev7:latest",
                "prompt": prompt,
                "stream": True
            }
            try:
                async with session.post(OLLAMA_URL, headers=headers, json=payload) as resp:
                    print(f"Status code: {resp.status}")
                    async for line in resp.content:
                        if line:
                            try:
                                chunk = line.decode("utf-8").strip()
                                if chunk:
                                    print("Chunk:", chunk)
                                    await websocket.send(chunk)
                            except Exception as e:
                                print("Error sending chunk:", e)
            except Exception as e:
                print("Failed to send request to Ollama:", e)
                error_response = json.dumps({
                    "response": " Gagal konek ke Ollama. Cek apakah model aktif dan Ollama jalan.",
                    "done": True
                })
                await websocket.send(error_response)


async def main():
    print("WebSocket server running at ws://localhost:8765")
    async with websockets.serve(handle_ws, "localhost", 8765):
        await asyncio.Future()  # run forever

if __name__ == "__main__":
    asyncio.run(main())
