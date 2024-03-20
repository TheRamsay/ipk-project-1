import socket
import struct
from dataclasses import dataclass
import time
import asyncio

@dataclass
class AuthMessage:
    message_id: int
    username: str
    display_name: str
    secret: str

@dataclass
class JoinMessage:
    message_id: int
    channel: str
    display_name: str

@dataclass
class MessageMessage:
    message_id: int
    display_name: str
    content: str

@dataclass
class ErrorMessage:
    message_id: int
    display_name: str
    content: str

@dataclass
class ReplyMessage:
    message_id: int
    result: int
    ref_message_id: int
    message_content: str

@dataclass
class ByeMessage:
    message_id: int

@dataclass
class ConfirmMessage:
    ref_message_id: int

def parse_strings(data: bytes) -> list[str]:
    null_positions = [i for i, char in enumerate(data) if char == 0]
    strings = [data[start+1:end].decode("ascii") for start, end in zip([-1] + null_positions, null_positions)]

    return strings

class UdpServer:
    def __init__(self, host, port):
        self.host = host
        self.port = port
        self.auth_socket = None
        self.msg_socket = None
        self.msg_id = 0
        self.processed_messages: set[int] = set()
        self.pending_messages: dict[int, int] = dict()

    def run(self):
        self.loop = asyncio.get_event_loop()

        self.auth_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.auth_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.auth_socket.bind((self.host, self.port))

        self.msg_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.msg_socket.bind((self.host, 4568))
        print(f"UDP server listening on 😳 {self.host}:{self.port}")

        while True:
            print("Waiting for message...")
            data, client_address = self.auth_socket.recvfrom(100)  # Adjust buffer size as needed
            decoded_message = self.decode_message(data)
            print(f"Received message from {client_address}: {decoded_message}")

            if not isinstance(decoded_message, ConfirmMessage):
                if decoded_message.message_id == 0:
                    print("Sleeping for 0.6 seconds")
                    time.sleep(1.5)

                print("BLEEEE")
                self.confirm_message(decoded_message.message_id, client_address)

                if decoded_message.message_id in self.processed_messages:
                    continue

                self.processed_messages.add(decoded_message.message_id)
                print("Sent confirmation")

            if isinstance(decoded_message, ConfirmMessage):
                if decoded_message.ref_message_id in self.pending_messages:
                    del self.pending_messages[decoded_message.ref_message_id]

            if isinstance(decoded_message, AuthMessage):
                msg = f"Welcome to the chat server {decoded_message.display_name}!"
                fmt = f"!B H B H {len(msg)}s B"

                data = struct.pack(fmt, 0x01, self.msg_id, 0x01, decoded_message.message_id, bytes(msg, "ascii") , 0x00)
                self.send_message(data, client_address)
                self.msg_id += 1

            elif isinstance(decoded_message, JoinMessage):
                msg = f"Wellcome to room {decoded_message.channel}"                    
                fmt = f"!B H B H {len(msg)}s B"
                data = struct.pack(fmt, 0x01, self.msg_id, 0x01, decoded_message.message_id, bytes(msg, "ascii") , 0x00)
                self.send_message(data, client_address)
                self.msg_id += 1

            elif isinstance(decoded_message, MessageMessage):
                msg = decoded_message.content
                display_name = decoded_message.display_name
                fmt = f"!B H {len(display_name)}s B {len(msg)}s B"
                data = struct.pack(fmt, 0x04, self.msg_id, bytes(display_name, "ascii"), 0x00, bytes(msg, "ascii"), 0x00)
                self.send_message(data, client_address)
                self.msg_id += 1

            elif isinstance(decoded_message, ByeMessage):
                self.auth_socket.close()
                return

    def decode_message(self, data):
        message_type = struct.unpack('!B', data[0:1])[0]

        if message_type == 0x00:  # CONFIRM
            ref_message_id = struct.unpack('!H', data[1:3])[0]
            return ConfirmMessage(ref_message_id)

        elif message_type == 0x01:  # REPLY
            message_id, result, ref_message_id = struct.unpack('!HBH', data[1:8])
            message_contents = parse_strings(data[8:])
            return ReplyMessage(message_id, result, ref_message_id, message_contents)

        elif message_type == 0x02:  # AUTH
            message_id = struct.unpack('!H', data[1:3])[0]
            username, display_name, secret = parse_strings(data[3:])
            return AuthMessage(message_id, username, display_name, secret)

        elif message_type == 0x03: # JOIN
            message_id = struct.unpack('!H', data[1:3])[0]
            channel, display_name = parse_strings(data[3:])
            return JoinMessage(message_id, channel, display_name)
        
        elif message_type == 0x04:
            message_id = struct.unpack('!H', data[1:3])[0]
            display_name, content = parse_strings(data[3:])
            return MessageMessage(message_id, display_name, content)
        
        elif message_type == 0xFE:
            message_id = struct.unpack('!H', data[1:3])[0]
            display_name, content = parse_strings(data[3:])
            return ErrorMessage(message_id, display_name, content)

        elif message_type == 0xFF:
            message_id = struct.unpack('!H', data[1:3])[0]
            return ByeMessage(message_id)
        else:
            None

    def send_message(self, data, client_address):
        self.auth_socket.sendto(data, client_address)

        if data[0] != 0x00:
            if (msg_id := struct.unpack('!H', data[1:3])[0]) not in self.pending_messages:
                self.pending_messages[msg_id] = 0

            self.loop.create_task(self.timeout_expired(data, client_address))

    def confirm_message(self, ref_message_id, client_address):
        data = struct.pack('!B H', 0x00, ref_message_id)
        self.send_message(data, client_address)

    async def timeout_expired(self, data, client_address):
        await asyncio.sleep(0.250)

        msg_id = struct.unpack('!H', data[1:3])[0]

        if msg_id in self.pending_messages:
            await self.send_message(data, client_address)
            self.pending_messages[msg_id] += 1

            if self.pending_messages[msg_id] > 3:
                raise Exception("Message could not be delivered.")
        else:
            print("Message was delivered successfully")


if __name__ == "__main__":
    udp_server = UdpServer("0.0.0.0", 4567)  # Replace with your server IP and port
    udp_server.run()
