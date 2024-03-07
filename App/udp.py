import socket
import struct

def parse_strings(data: bytes) -> list[str]:
    null_positions = [i for i, char in enumerate(data) if char == 0]
    strings = [data[start+1:end].decode("ascii") for start, end in zip([-1] + null_positions, null_positions)]

    return strings

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
                decoded_message = self.decode_message(data)
                print(f"Received message from {client_address}: {decoded_message}")


                if decoded_message.startswith("AUTH"):
                    fmt = f"!B H B H {len('Cauki mnauki :3')}s B"

                    data = struct.pack(fmt, 0x01, 0x00, 0x01, 0x00, b"Cauki mnauki :3", 0x00)
                    print(data)
                    udp_socket.sendto(data, client_address)


    def decode_message(self, data):
        message_type = struct.unpack('!B', data[0:1])[0]

        if message_type == 0x00:  # CONFIRM
            ref_message_id = struct.unpack('!H', data[1:3])[0]
            return f"CONFIRM - Ref_MessageID: {ref_message_id}"

        elif message_type == 0x01:  # REPLY
            message_id, result, ref_message_id = struct.unpack('!HBH', data[1:8])
            message_contents = parse_strings(data[8:])
            return f"REPLY - MessageID: {message_id}, Result: {result}, Ref_MessageID: {ref_message_id}, MessageContents: {message_contents}"

        elif message_type == 0x02:  # AUTH
            message_id = struct.unpack('!H', data[1:3])[0]
            username, display_name, secret = parse_strings(data[3:])
            return f"AUTH - MessageID: {message_id}, Username: {username}, DisplayName: {display_name}, Secret: {secret}"

        elif message_type == 0x03: # JOIN
            message_id = struct.unpack('!H', data[1:3])[0]
            channel, display_name = parse_strings(data[3:])
            return f"JOIN - MessageID: {message_id}, Channel: {channel}, DisplayName: {display_name}"
        
        elif message_type == 0x04:
            message_id = struct.unpack('!H', data[1:3])[0]
            display_name, content = parse_strings(data[3:])
            return f"MSG - MessageID: {message_id}, DisplayName: {display_name}, Content: {content}"
        
        elif message_type == 0xFE:
            message_id = struct.unpack('!H', data[1:3])[0]
            display_name, content = parse_strings(data[3:])
            return f"ERR - MessageID: {message_id}, DisplayName: {display_name}, Content: {content}"

        elif message_type == 0xFF:
            message_id = struct.unpack('!H', data[1:3])[0]
            return f"BYE - MessageID: {message_id}"

        else:
            return "Unknown message type"

if __name__ == "__main__":
    udp_server = UdpServer("0.0.0.0", 4567)  # Replace with your server IP and port
    udp_server.run()
