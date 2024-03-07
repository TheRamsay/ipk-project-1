import socket
import struct

class UdpServer:
    def __init__(self, host, port):
        self.host = host
        self.port = port

    def run(self):
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp_socket:
            udp_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            udp_socket.bind((self.host, self.port))
            print(f"UDP server listening on 😳 {self.host}:{self.port}")

            while True:
                print("Waiting for message...")
                data, client_address = udp_socket.recvfrom(100)  # Adjust buffer size as needed
                print(f"Received message from {client_address}: {data}")
                decoded_message = self.decode_message(data)
                print(f"Received message from {client_address}: {decoded_message}")

    def decode_message(self, data):
        message_type = struct.unpack('!B', data[0:1])[0]

        if message_type == 0x00:  # CONFIRM
            ref_message_id = struct.unpack('!H', data[1:3])[0]
            return f"CONFIRM - Ref_MessageID: {ref_message_id}"

        elif message_type == 0x01:  # REPLY
            message_id, result, ref_message_id, message_contents = struct.unpack('!HBB', data[1:8])
            message_contents = message_contents.rstrip(b'\x00').decode('utf-8')
            return f"REPLY - MessageID: {message_id}, Result: {result}, Ref_MessageID: {ref_message_id}, MessageContents: {message_contents}"

        elif message_type == 0x02:  # AUTH
            message_id, username, display_name, secret = struct.unpack('!H50s50s50s', data[1:104])
            username = username.rstrip(b'\x00').decode('utf-8')
            display_name = display_name.rstrip(b'\x00').decode('utf-8')
            secret = secret.rstrip(b'\x00').decode('utf-8')
            return f"AUTH - MessageID: {message_id}, Username: {username}, DisplayName: {display_name}, Secret: {secret}"

        else:
            return "Unknown message type"

if __name__ == "__main__":
    udp_server = UdpServer("127.0.0.1", 4567)  # Replace with your server IP and port
    udp_server.run()
