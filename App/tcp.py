import asyncio
from dataclasses import dataclass
from typing import List
from loguru import logger

@dataclass
class Client:
    username: str
    display_name: str
    password: str
    current_room: str
    writer: asyncio.StreamWriter
    reader: asyncio.StreamReader

class TCPServer:
    def __init__(self, host: str, port: int):
        self.host = host
        self.port = port
        self.clients: List[Client] = []

    async def handle_client(self, reader, writer):
        logger.debug(f"New client connected ({writer.get_extra_info('peername')})")
        client = None

        while True:
            data = await reader.read(1024)
            if not data:
                break

            message = data.decode('ascii').strip()
            logger.debug(f"Received message: {message}")

            match message.split(" "):
                case ["AUTH", username, "AS", display_name, "USING", password]:
                    logger.debug(f"Authenticating user {username} as {display_name} using password {password}")
                    client = Client(username, display_name, password, "lobby", writer, reader)
                    self.clients.append(client)
                    await self.send_message(f"REPLY OK IS Welcome to the chat server {display_name}!\r\n", writer)
                    await self.broadcast(f"MSG FROM Server IS {client.display_name} has joined room {client.current_room}\r\n", client.current_room, None)
                    # print("Sending error")
                    # await self.send_message(f"ERR FROM Server IS This is a test error message\r\n", writer)
                    # print("Sent error")

                case ["JOIN", channel, "AS", display_name]:
                    print("Joining room")
                    client.display_name = display_name
                    logger.debug(f"User {client.display_name} is joining room {channel}")
                    client.current_room = channel
                    await self.send_message(f"REPLY OK IS Welcome to room {channel}\r\n", writer)
                    await self.broadcast(f"MSG FROM Server IS {client.display_name} has joined room {client.current_room}\r\n", client.current_room, None)
                case ["MSG", "FROM", display_name, "IS", *content]:
                    await self.broadcast(f"MSG FROM {display_name} IS {' '.join(content)}\r\n", client.current_room, client)
                case ["ERROR", "FROM", display_name, "IS", *content]:
                    logger.error(f"Received error from {display_name}: {' '.join(content)}")
                    await self.send_message(f"BYE\r\n", writer)
                case ["BYE"]:
                    logger.debug(f"User {client.display_name} has disconnected")
                    break
                case _:
                    raise ValueError(f"Invalid message: {message}")

        logger.debug("Client disconnected")
        # Remove the client when they disconnect
        self.clients.remove(writer)

    async def broadcast(self, message: str, channel: str = "lobby", from_client: Client = None):
        print(self.clients)
        for client in self.clients:
            if client.current_room != channel or (from_client and client.display_name == from_client.display_name):
                continue
            try:
                client.writer.write(message.encode('ascii'))
            except asyncio.CancelledError as e:
                print("Error broadcasting message", e)

    async def send_message(self, message: str, writer: asyncio.StreamWriter):
        writer.write(message.encode('ascii'))
        # await writer.drain() 

    async def start_server(self):
        logger.info(f"Starting server on {self.host}:{self.port}")
        server = await asyncio.start_server(
            self.handle_client, self.host, self.port
        )

        async with server:
            await server.serve_forever()

if __name__ == "__main__":
    server_host = '127.0.0.1'
    server_port = 4567

    tcp_server = TCPServer(server_host, server_port)

    try:
        asyncio.run(tcp_server.start_server())
    except KeyboardInterrupt:
        print("Server stopped by the user.")
