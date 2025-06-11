import socket
import struct
import numpy as np
import soundfile as sf
import whisper
import io

# Load Whisper once (startup)
model = whisper.load_model("medium", device = "cpu")

def receive_all(sock, size):
    """Terima semua data dengan ukuran yang diharapkan."""
    data = bytearray()
    while len(data) < size:
        packet = sock.recv(size - len(data))
        if not packet:
            raise ConnectionError("Connection lost while receiving data")
        data.extend(packet)
    return data

def process_audio(wav_bytes):
    """Proses audio WAV menjadi float32 mono 16kHz."""
    audio_stream = io.BytesIO(wav_bytes)
    audio, sr = sf.read(audio_stream, dtype='int16')

    # Normalize to float32
    audio = audio.astype(np.float32) / 32768.0

    # Transcribe langsung (Unity sudah kirim 16kHz mono)
    result = model.transcribe(audio, language='en')
    return result["text"]

def main():
    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(("0.0.0.0", 5000))
    server.listen(5)

    print("Listening on port 5000...")

    while True:
        client, addr = server.accept()
        print(f"Connection from {addr}")

        try:
            data_len = struct.unpack('<I', receive_all(client, 4))[0]
            audio_data = receive_all(client, data_len)

            transcription = process_audio(audio_data)
            print(f"Transcription: {transcription}")

            client.sendall((transcription.strip() + "\n").encode('utf-8'))

        except Exception as e:
            print(f"Error: {e}")
            client.sendall(f"Error: {e}\n".encode('utf-8'))

        finally:
            client.close()

if __name__ == "__main__":
    main()
