import cv2
import mediapipe as mp
import socket
import json
import threading
import base64
import time

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
        
        # Inicializar webcam
        self.cap = cv2.VideoCapture(0)
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, 640)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, 480)
        self.cap.set(cv2.CAP_PROP_FPS, 30)
        
    def start_server(self):
        """Inicia el servidor TCP"""
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
    
    def get_nose_position(self, landmarks, frame_width, frame_height):
        """Obtiene la posición de la punta de la nariz"""
        if not landmarks:
            return None
            
        # Índice 1 corresponde a la punta de la nariz en MediaPipe Face Mesh
        # Otros puntos de referencia de la nariz:
        # 1: Punta de la nariz (nose tip)
        # 2: Nariz centro
        # 19: Izquierda de la nariz
        # 20: Derecha de la nariz
        
        nose_tip_idx = 1  # Punta de la nariz
        
        if nose_tip_idx < len(landmarks.landmark):
            nose_landmark = landmarks.landmark[nose_tip_idx]
            x = nose_landmark.x * frame_width
            y = nose_landmark.y * frame_height
            z = nose_landmark.z  # Profundidad relativa
            
            return (x, y, z)
        
        return None
    
    def process_and_send(self, client_socket):
        """Procesa los frames y envía datos a Unity"""
        frame_count = 0
        
        while self.running:
            ret, frame = self.cap.read()
            if not ret:
                print("Error al capturar frame")
                break
            
            frame_count += 1
            
            # Voltear horizontalmente para efecto espejo
            frame = cv2.flip(frame, 1)
            frame_height, frame_width = frame.shape[:2]
            
            # Convertir BGR a RGB para MediaPipe
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = self.face_mesh.process(rgb_frame)
            
            head_position = None
            
            # Detectar posición de la nariz
            if results.multi_face_landmarks:
                for face_landmarks in results.multi_face_landmarks:
                    nose_pos = self.get_nose_position(face_landmarks, frame_width, frame_height)
                    if nose_pos:
                        head_position = {
                            'x': nose_pos[0],
                            'y': nose_pos[1],
                            'z': nose_pos[2],  # Profundidad
                            'normalized_x': nose_pos[0] / frame_width,
                            'normalized_y': nose_pos[1] / frame_height
                        }
                        
                        # Frame se mantiene limpio, sin marcadores visuales
                        break
            
            # Codificar frame a base64 (reducir calidad para mejorar performance)
            _, buffer = cv2.imencode('.jpg', frame, [cv2.IMWRITE_JPEG_QUALITY, 60])
            frame_b64 = base64.b64encode(buffer).decode('utf-8')
            
            # Preparar datos para enviar
            data = {
                'frame_id': frame_count,
                'timestamp': time.time(),
                'head_position': head_position,
                'frame_data': frame_b64,
                'frame_width': frame_width,
                'frame_height': frame_height
            }
            
            try:
                # Convertir a JSON y enviar
                json_data = json.dumps(data)
                message = f"{len(json_data)}:{json_data}"
                client_socket.send(message.encode('utf-8'))
                
                # No mostrar ventana local - Solo Unity maneja la visualización
                # cv2.imshow('Nose Tracker - Python', frame)
                # if cv2.waitKey(1) & 0xFF == ord('q'):
                #     break
                    
            except Exception as e:
                print(f"Error enviando datos: {e}")
                break
        
        client_socket.close()
    
    def cleanup(self):
        """Limpia recursos"""
        self.running = False
        if self.cap:
            self.cap.release()
        if self.socket:
            self.socket.close()
        # cv2.destroyAllWindows() - No hay ventanas que cerrar
        print("Recursos liberados")

if __name__ == "__main__":
    tracker = HeadTracker()
    try:
        tracker.start_server()
    except KeyboardInterrupt:
        print("\nInterrumpido por usuario")
    finally:
        tracker.cleanup()