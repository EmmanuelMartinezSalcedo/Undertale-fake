import cv2
import mediapipe as mp
import socket
import json
import base64

class HeadTracker:
    def __init__(self, host='localhost', port=12345):
        self.host = host
        self.port = port
        self.socket = None
        self.running = False
        
        # Inicializar MediaPipe
        self.mp_face_mesh = mp.solutions.face_mesh
        self.face_mesh = self.mp_face_mesh.FaceMesh(
            static_image_mode=False,
            max_num_faces=1,
            refine_landmarks=True,
            min_detection_confidence=0.5,
            min_tracking_confidence=0.5
        )
        
        # Inicializar webcam (resolución nativa)
        self.cap = cv2.VideoCapture(0)
        if not self.cap.isOpened():
            raise RuntimeError("No se pudo abrir la cámara")
        
    def start_server(self):
        try:
            self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.socket.bind((self.host, self.port))
            self.socket.listen(1)
            print(f"Servidor iniciado en {self.host}:{self.port}")
            print("Esperando conexión de Unity...")
            
            client_socket, addr = self.socket.accept()
            print(f"Unity conectado desde: {addr}")
            
            self.running = True
            self.process_and_send(client_socket)
        except Exception as e:
            print(f"Error en servidor: {e}")
        finally:
            self.cleanup()
    
    def get_nose_position(self, landmarks):
        """Devuelve coordenadas normalizadas de la punta de la nariz"""
        nose_tip_idx = 1  # Punto de la nariz
        if nose_tip_idx < len(landmarks.landmark):
            nose = landmarks.landmark[nose_tip_idx]
            return {
                'normalized_x': nose.x,
                'normalized_y': nose.y
            }
        return None
    
    def process_and_send(self, client_socket):
        # Obtener una vez las dimensiones del frame
        ret, frame = self.cap.read()
        if not ret:
            print("No se pudo capturar el primer frame.")
            return

        frame = cv2.flip(frame, 1)
        frame_height, frame_width = frame.shape[:2]

        # Enviar las dimensiones solo una vez al inicio
        init_data = {
            'init': True,
            'frame_width': frame_width,
            'frame_height': frame_height
        }
        try:
            init_json = json.dumps(init_data)
            init_message = f"{len(init_json)}:{init_json}"
            client_socket.send(init_message.encode('utf-8'))
        except Exception as e:
            print(f"Error enviando datos iniciales: {e}")
            return

        # Comienza el bucle principal de envío de frames y posiciones
        while self.running:
            ret, frame = self.cap.read()
            if not ret:
                print("Error al capturar frame")
                break

            frame = cv2.flip(frame, 1)
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = self.face_mesh.process(rgb_frame)

            head_position = None
            if results.multi_face_landmarks:
                head_position = self.get_nose_position(results.multi_face_landmarks[0])

            _, buffer = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 60])
            frame_b64 = base64.b64encode(buffer).decode('utf-8')

            data = {
                'head_position': head_position,
                'frame_data': frame_b64
            }

            try:
                json_data = json.dumps(data)
                message = f"{len(json_data)}:{json_data}"
                client_socket.send(message.encode('utf-8'))
            except Exception as e:
                print(f"Error enviando datos: {e}")
                break

        client_socket.close()

    
    def cleanup(self):
        self.running = False
        if self.cap:
            self.cap.release()
        if self.socket:
            self.socket.close()
        print("Recursos liberados")

if __name__ == "__main__":
    tracker = HeadTracker()
    try:
        tracker.start_server()
    except KeyboardInterrupt:
        print("\nInterrumpido por usuario")
    finally:
        tracker.cleanup()
