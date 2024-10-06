import socket
import threading
import time
from flask import Flask, request, jsonify, url_for
from werkzeug.serving import make_server

# Call this from command line to send messages to this script
# curl -X POST http://localhost:5000/stop

app = Flask(__name__)
server = None
stop_event = threading.Event()

@app.route('/add', methods=['POST'])
def add():
    print('add called')
    return "Success", 200

@app.route('/stop', methods=['POST'])
def stop():
    if server is not None:
        stop_event.set()
        return "Stopping the server...", 200
    else:
        return "Server is not running.", 400

if __name__ == '__main__':
    print('before run')

    ip = '127.0.0.1'
    port = 5000

    while True:
        try:
            # Create a socket and bind it to the desired IP address and port
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.bind((ip, port))
            sock.close()        # need to close, or the call to make_server will fail
            print(f'found free port: {port}')
            break
        except socket.error as e:
            print(f"Port {port} is already in use, trying next one...")
            port += 1

    server = make_server(ip, port, app)
    print(f'* Running on http://{ip}:{port}')

    server_thread = threading.Thread(target=server.serve_forever)
    server_thread.start()
    print('after run')

    stop_event.wait()

    print('shutting down')
    server.shutdown()
    print('shut down')