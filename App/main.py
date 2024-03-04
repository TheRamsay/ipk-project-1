import socket
from dataclasses import dataclass

@dataclass
class Client:
    username: str
    display_name: str
    password: str
    current_room: str

    def __hash__(self) -> int:
        return hash(self.display_name)


CLIENTS = {}

def start_server():
    host = '127.0.0.1'
    port = 4567 

    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server_socket.bind((host, port))
    server_socket.listen(5)

    print(f"Server listening on {host}:{port}")

    try:
        while True:
            client_socket, client_address = server_socket.accept()
            print(f"Accepted connection from {client_address}")

            handle_client(client_socket)
    except Exception as e:
        e.with_traceback()
    finally:
        print("Server shutting down")
        server_socket.close()

def handle_client(client_socket):
    while True:
        data = client_socket.recv(1024)
        if not data:
            break

        message = data.decode('utf-8')
        message = message.strip()

        match message.split(" "):
            case ["AUTH", username, "AS", display_name, "USING",password]:
                if username in CLIENTS:
                    client_socket.sendall("REPLY NOK IS Username already taken\r\n".encode('utf-8'))
                    client_socket.close()
                    return
                
                client = Client(username, display_name, password, "lobby")
                client_socket.sendall(f"REPLY OK IS Welcome to the chat server {display_name}!\r\n".encode('utf-8'))
                for c in CLIENTS:
                    if c.current_room == client.current_room:
                        client_socket.sendall(f"MSG FROM Server IS {c.display_name} has joined room {client.current_room}\r\n".encode('utf-8'))
                CLIENTS[display_name] = client
            case ["JOIN", channel, "AS", display_name]:
                client = CLIENTS[display_name]
                client.current_room = channel
                client_socket.sendall(f"REPLY OK IS Welcome to room {channel}\r\n".encode('utf-8'))
                for c in CLIENTS.values():
                    if c.current_room == client.current_room:
                        client_socket.sendall(f"MSG FROM Server IS {c.display_name} has joined room {client.current_room}\r\n".encode('utf-8'))
            case ["MSG", "FROM", display_name, "IS", *content]:
                print(f"Message from {display_name}: {' '.join(content)}")
                for _, c in CLIENTS.items():
                    if c.current_room == client.current_room and c.display_name != display_name:
                        client_socket.sendall(f"MSG FROM {display_name} IS {' '.join(content)}\r\n".encode('utf-8'))
            case ["ERROR", "FROM", display_name, "IS", *content]:
                client_socket.sendall(f"BYE\r\n".encode('utf-8'))
            case ["BYE"]:
                print("Received bye 😭")
                client_socket.close()
                return
            case _:
                raise ValueError(f"Invalid message: {message}")

    client_socket.close()
    print("Client connection closed")

if __name__ == "__main__":
    start_server()
