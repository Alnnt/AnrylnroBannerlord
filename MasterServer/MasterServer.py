from flask import Flask, request, Response
import threading
import logging
import json
import os

app = Flask(__name__)

DATA_FILE = "servers.json"
lock = threading.Lock()
servers = {}  # {(ip, gamePort): httpPort}

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s"
)


# ---------- persistence ----------

def load_data():
    global servers
    if os.path.exists(DATA_FILE):
        with open(DATA_FILE, "r") as f:
            data = json.load(f)
            servers = {(i["ip"], i["gamePort"]): i["httpPort"] for i in data}
        logging.info("Loaded %d servers", len(servers))


def save_data():
    data = [
        {"ip": k[0], "gamePort": k[1], "httpPort": v}
        for k, v in servers.items()
    ]
    with open(DATA_FILE, "w") as f:
        json.dump(data, f)


# ---------- helpers ----------

def get_params():
    return (
        request.args.get("publicIp"),
        request.args.get("gamePort"),
        request.args.get("httpPort"),
    )


# ---------- routes ----------

@app.route("/bannerlord/register")
def register():
    ip, game_port, http_port = get_params()
    if not ip or not game_port or not http_port:
        return "missing parameters", 400

    with lock:
        servers[(ip, game_port)] = http_port
        save_data()

    logging.info("REGISTER %s:%s -> %s", ip, game_port, http_port)
    return "ok"


@app.route("/bannerlord/unregister")
def unregister():
    ip, game_port, _ = get_params()

    with lock:
        servers.pop((ip, game_port), None)
        save_data()

    logging.info("UNREGISTER %s:%s", ip, game_port)
    return "ok"


@app.route("/bannerlord/query")
def query():
    address = request.args.get("address")
    game_port = request.args.get("gamePort")

    with lock:
        http_port = servers.get((address, game_port))

    if not http_port:
        return Response("", status=404)

    return f"{address}:{http_port}", 200, {"Content-Type": "text/plain"}


# ---------- main ----------

if __name__ == "__main__":
    load_data()
    app.run(host="0.0.0.0", port=7015, threaded=True)
