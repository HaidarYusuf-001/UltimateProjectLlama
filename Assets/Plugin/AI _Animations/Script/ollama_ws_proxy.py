import asyncio
import json
import websockets
import aiohttp

# --- UBAH URL KE ENDPOINT CHAT ---
OLLAMA_URL = "http://127.0.0.1:11434/api/chat"

async def handle_ws(websocket):
    async for message in websocket:
        prompt = message
        print(f"Received prompt: {prompt}")

        async with aiohttp.ClientSession() as session:
            # --- UBAH STRUKTUR PAYLOAD ---
            payload = {
                "model": "umm-informatics-q5_k_m-gpu:latest",
                "messages": [
                    {
                        "role": "user",
                        "content": prompt
                    }
                ],
                "stream": True,
                "options": {
                    "temperature": 0.5
                }
            }
            # ---------------------------
            # ... sisa kode tetap sama ...
            try:
                async with session.post(OLLAMA_URL, headers={"Content-Type": "application/json"}, json=payload) as resp:
                    print(f"Status code from Ollama: {resp.status}")
                    async for line in resp.content:
                        if line:
                            try:
                                chunk_str = line.decode("utf-8").strip()
                                if chunk_str:
                                    # Untuk /api/chat, format chunk-nya sedikit berbeda
                                    # kita perlu parse JSON-nya untuk mendapatkan content
                                    json_data = json.loads(chunk_str)
                                    content = json_data.get("message", {}).get("content", "")

                                    # Kita kirim kembali dalam format JSON sederhana
                                    await websocket.send(json.dumps({
                                        "response": content,
                                        "done": json_data.get("done", False)
                                    }))
                            except (UnicodeDecodeError, json.JSONDecodeError) as e:
                                print(f"Skipping malformed chunk: {line}, Error: {e}")
                                continue
            except aiohttp.ClientError as e:
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