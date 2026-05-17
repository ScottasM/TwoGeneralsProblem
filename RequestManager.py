
import gevent
import gevent.monkey
gevent.monkey.patch_all()

from flask import Flask, request, jsonify, render_template



import uuid
import ssl
import time
from flask_socketio import SocketIO
from threading import Lock
import logging




app = Flask(__name__)
socketio = SocketIO(app, host='0.0.0.0', port=5000, cors_allowed_origins='*',async_mode='gevent')  # Allows connections from any IP address

applications = {}
#app.logger.setLevel(logging.DEBUG)

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

"""
context = SSL.Context(SSL.TLSv1_2_METHOD)
context.use_privatekey_file('/etc/ssl/private/private-unencrypted.key')
context.use_certificate_file('/etc/ssl/certificate.crt')
context.check_privatekey()  # Optional: Check if the key matches the certificate
"""

# context = ssl.SSLContext(ssl.PROTOCOL_TLSv1_2)
# context.load_cert_chain('/etc/ssl/chained.pem', '/etc/ssl/private/private-unencrypted.key')

connected_clients = {}

@socketio.on('connect')
def handle_connect():
    print("Connection established!")
    
@socketio.on('MakeRoom') 
def make_room(data):
    splitted = data.split('/')
    aplication_id = splitted[0]
    loby_id = splitted[1]
    hosted = int(splitted[2])
    user_id = uuid.uuid4().hex

    client_id = request.sid
    


    if(hosted == 0):
        print("peer connecting")
        host_id,host_client_id = find_host(aplication_id,loby_id)
        if(host_client_id == None):
            socketio.emit("HostNotFound","",room = client_id)
            print("Host not found for user")
            return
        

        #print(f"host_client_id : {host_client_id}" )
        socketio.emit('PeerConnected',user_id,room=host_client_id)
    else:
        print(f"Created host : {client_id}")
    connected_clients[(aplication_id, loby_id,hosted,user_id)] = client_id
    print(f"got data : {aplication_id} + {loby_id} + {client_id}")

@socketio.on('SendOffer')
def send_offer(data):

    splitted = data.split(';/;/;/')
    aplication_id = splitted[0]
    loby_id = splitted[1]
    guid = splitted[2]
    sdp = splitted[3]
    
    client_id = get_client_id(aplication_id,loby_id,guid)
    myguid = get_guid(aplication_id,loby_id,request.sid)

    print(f"sending Offer from {request.sid}|{myguid} to {client_id}|{guid}")

    if client_id:
        message = sdp + ";;//" + myguid
        socketio.emit('OfferReceived', message, room=client_id)
        return f'Message sent to Unity client with Application ID: {aplication_id} and Lobby ID: {loby_id}'
    else:
        return 'Client not found for the given IDs in send_offer'

@socketio.on('SendICE')
def send_ice(data):
    #print("send_ice : " + data)
    splitted = data.split(';/;/;/')
    aplication_id = splitted[0]
    loby_id = splitted[1]
    guid = splitted[2]
    ice = splitted[3]
    
    client_id = get_client_id(aplication_id,loby_id,guid)
    myguid = get_guid(aplication_id,loby_id,request.sid)

    print(f"sending ice from {request.sid}|{myguid} to {client_id}|{guid}")

    if client_id:
        message = ice + ";;//" + myguid
        socketio.emit('IceGot', message, room=client_id)
        return f'Message sent to Unity client with Application ID: {aplication_id} and Lobby ID: {loby_id}'
    else:
        return 'Client not found for the given IDs in send_ice'

clients_lock = Lock()

@socketio.on('disconnect')
def handle_disconnect():
    print("Received disconnect")
    sid = request.sid

    with clients_lock:
        for (app_id, lobby_id, host, user_id), client_id in connected_clients.copy().items():
            if client_id == sid:
                try:
                    if host == 1:
                        host_disconnected(app_id, lobby_id)
                    del connected_clients[(app_id, lobby_id, host, user_id)]
                except KeyError:
                    print("user not found in dictionary")
                    pass
                break

def host_disconnected(appId,lobbyId):
    clients_to_disconnect = []
    for key, client_id in connected_clients.copy().items():
        app_id, lobby_id, hosted, user_id = key
        if app_id == appId and lobbyId == lobby_id and hosted == 0:
            clients_to_disconnect.append(client_id)
    
    for client_id in clients_to_disconnect:
        socketio.emit('HostDisconnected', "", room=client_id)
        #socketio.disconnect(client_id)



@socketio.on('SendAnswer')
def handle_post_request(data):
    #print("Sending answer:" + data)
    splitted = data.split(';/;/;/')
    aplication_id = splitted[0]
    loby_id = splitted[1]
    guid = splitted[2]
    sdp = splitted[3]
    
    host_id, client_id = find_host(aplication_id,loby_id)

    

    if(client_id == None):
        socketio.emit("HostNotFound","",room = client_id)
        print("Host not found for user on answer creation")
        return


    myguid = get_guid(aplication_id,loby_id,request.sid)

    print(f"sending answer from {request.sid}|{myguid} to {client_id}|{guid}")

    message = sdp + ";/;/;/" + myguid
    socketio.emit('AnswerCreated', message, room=client_id)

def get_guid(application_id, lobby_id, client_id):
    for key, client_ids in connected_clients.items():
        if key[0] == application_id and key[1] == lobby_id and client_ids == client_id:
            return key[3]
    return None

def get_client_id(application_id, lobby_id, user_id):
    for key, client_id in connected_clients.items():
        if key[0] == application_id and key[1] == lobby_id and key[3] == user_id:
            return client_id
    return None
def find_host(application_id,lobby_id):
    for key, client_id in connected_clients.items():
        print(f"searching for host keys : {key[0]} == {application_id} {key[1]} == {lobby_id} and host == {key[2]}")
        if key[0] == application_id and key[1] == lobby_id and key[2] == 1:
            user_id = key[3]
            return user_id, client_id
    return None, None


# Function to add a new application
def add_application(application_id):
    if application_id not in applications:
        applications[application_id] = []

# Function to add a lobby to an application
def add_lobby(application_id, lobby_id, lobby_info):
    if application_id in applications:
        applications[application_id].append({"lobby_id": lobby_id, "lobby_info": lobby_info, "last_update": int(time.time())})
        return 1
    else:
        return 2

# Function to delete a lobby from an application
def delete_lobby(application_id, lobby_id):
    if application_id in applications:
        lobbies = applications[application_id]
        for lobby in lobbies:
            if lobby["lobby_id"] == lobby_id:
                lobbies.remove(lobby)
                return 0 
        return 1
    else:
        return 2

# Function to update lobby information
def update_lobby(application_id, lobby_id, new_lobby_info):
    if application_id in applications:
        lobbies = applications[application_id]
        for lobby in lobbies:
            if lobby["lobby_id"] == lobby_id:
                lobby["lobby_info"] = new_lobby_info
                return 0
        return 1
    else:
        return 2

@app.route('/', methods=['GET','POST'])
def create_or_update_lobby():

    Action = -1
    try:
        Action = int(request.form.get('Action'))
    except: 
        form_data = request.form.to_dict()
        
        # Print the form data to the console (for debugging)
        print(form_data)
        print("Received request form without Action key : {form_data)}" )
        
    if(Action != -1):
        application_id = request.form.get('application_id')
        add_application(application_id)
        lobby_data = request.form.get('lobby_data')

        if Action == 0:
            lobby_id = uuid.uuid4().hex
            retValue = add_lobby(application_id,lobby_id,lobby_data)
            if retValue == 2:
                return "Application not found", 500
            if retValue == 1:
                return jsonify({'lobby_id': lobby_id}), 200

        lobby_id = request.form.get('lobby_id')
        if Action == 1:
            retValue = update_lobby(application_id,lobby_id,lobby_data)
            if retValue == 2:
                return "Application not found", 500
            if retValue == 1:
                return f"Lobby {lobby_id} not found in application {application_id}.", 500
            if retValue == 0:
                return f"Lobby {lobby_id} updated.", 200
        elif Action == 2:
            retValue = delete_lobby(application_id,lobby_id)
            if retValue == 2:
                return "Application not found", 500
            if retValue == 1:
                return f"Lobby {lobby_id} not found in application {application_id}.", 500
            if retValue == 0:
                return f"Lobby {lobby_id} deleted.", 200
        elif Action == 3:
            return jsonify(get_lobbies(application_id)), 200
        elif Action == 4:
            found = 0
            if application_id in applications:
                lobbies = applications[application_id]
                for lobby in lobbies:
                    #print(f"Comparing : --{lobby['lobby_id']}-- with --{lobby_id}--")
                    #print(f"types : {type(lobby['lobby_id']), {lobby_id}}")
                    if lobby['lobby_id'] == lobby_id:
                        lobby["last_update"] = int(time.time())
                        found = 1
                        break  
            if found == 1: 
                return f"Lobby {lobby_id} update time updated", 200
            elif found == 0 : 
                return f"Lobby with id {lobby_id} not found", 580
    else: return f"Action key not found in arguments", 400         


def get_lobbies(application_id):
    if application_id in applications:
        lobbies = applications[application_id]
        simplified_lobbies = [{"lobby_id": lobby["lobby_id"], "lobby_info": lobby["lobby_info"]} for lobby in lobbies]
        return simplified_lobbies
    else:
        return []


def print_lobbies_periodically():
    while True:
        print("All Lobbies:")
        for application_id, lobbies in applications.items():
            print(f"Application ID: {application_id}")
            for lobby in lobbies:
                print(f"Lobby ID: {lobby['lobby_id']}, Lobby Info: {lobby['lobby_info']}, Last Update: {lobby['last_update']}")
        time.sleep(5)  

def check_lobbies_periodically():
    while True:
        currentTime = int(time.time())
        for application_id, lobbies in applications.items():
            for lobby in lobbies:
                if(currentTime - int(lobby['last_update']) > 15):
                    print(f"Lobby in application : {application_id} with id : {lobby['lobby_id']} timed out")
                    delete_lobby(application_id,lobby['lobby_id'])
        time.sleep(5) 

# Start a new thread to run the print_lobbies_periodically() function
import threading

if __name__ == "__main__":
    print("Server starting...")
    socketio.run(app, host="0.0.0.0", port=5000)

threading.Thread(target=check_lobbies_periodically).start()




