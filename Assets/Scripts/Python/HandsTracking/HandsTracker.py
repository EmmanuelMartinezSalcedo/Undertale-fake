import cv2
import mediapipe as mp
import socket
import json
import base64

class HandTracker:
    def __init__(self, host='localhost', port=12345):
        self.host = host
        self.port = port
        self.socket = None
        self.running = False

        self.mp_hands = mp.solutions.hands
        self.hands = self.mp_hands.Hands(
            static_image_mode=False,
            max_num_hands=2,
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )

        self.cap = cv2.VideoCapture(0)
        if not self.cap.isOpened():
            raise RuntimeError("Camera not found.")

    def start_server(self):
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.socket.bind((self.host, self.port))
            self.socket.listen(1)
            print("Waiting for Unity connection...")

            client_socket, addr = self.socket.accept()
            print(f"Unity connected at: {addr}.")

            self.running = True
            self.process_and_send(client_socket)
        except Exception as e:
            print(f"Error at server: {e}")
        finally:
            self.cleanup()

    def get_hand_positions(self, multi_hand_landmarks, handedness):
        positions = {'left': None, 'right': None}
        for i, hand_landmarks in enumerate(multi_hand_landmarks):
            label = handedness[i].classification[0].label.lower()  # 'left' or 'right'
            wrist = hand_landmarks.landmark[self.mp_hands.HandLandmark.WRIST]
            positions[label] = {
                'normalized_x': wrist.x,
                'normalized_y': wrist.y
            }
        return positions

    def process_and_send(self, client_socket):
        while self.running:
            ret, frame = self.cap.read()
            if not ret:
                print("Error at getting frame.")
                break

            frame = cv2.flip(frame, 1)
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = self.hands.process(rgb_frame)

            hand_positions = {'left': None, 'right': None}
            if results.multi_hand_landmarks and results.multi_handedness:
                hand_positions = self.get_hand_positions(
                    results.multi_hand_landmarks,
                    results.multi_handedness
                )

            _, buffer = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 60])
            frame_b64 = base64.b64encode(buffer).decode('utf-8')

            data = {
                'hand_positions': hand_positions,
                'frame_data': frame_b64
            }

            try:
                json_data = json.dumps(data)
                message = f"{len(json_data)}:{json_data}"
                client_socket.send(message.encode('utf-8'))
            except Exception as e:
                print(f"Error sending data: {e}")
                break

        client_socket.close()

    def cleanup(self):
        self.running = False
        if self.cap:
            self.cap.release()
        if self.socket:
            self.socket.close()
        print("Finished processing, resources released.")

if __name__ == "__main__":
    tracker = HandTracker()
    try:
        tracker.start_server()
    except KeyboardInterrupt:
        print("\nUser interrupted the process.")
    finally:
        tracker.cleanup()
