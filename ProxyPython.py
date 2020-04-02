import socket
import json

# Do note that this is not perfect, and probably needs some work. Possibly load balancing effort onto other servers, etc

class NetworkFrame(dict):
    def __init__(self, m_Type, m_FrameId, m_TargetId, m_SenderId, m_Important, *args, **kwargs):
        self.m_Type = m_Type
        self.m_FrameId = m_FrameId
        self.m_TargetId = m_TargetId
        self.m_SenderId = m_SenderId
        self.m_Important = m_Important
        dict.__init__(self, m_Type=m_Type, m_FrameId=m_FrameId, m_TargetId=m_TargetId,
                      m_SenderId=m_SenderId, m_Important=m_Important)


server = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
server.bind(('127.0.0.1', 58120)) # Replace 127.0.0.1 with your IP, as well as port if thats changed
print("Proxy server created and binded")

frameCounter = 0
serverAddress = None
addressDictionary = {}


def sendFrame(msg, target):
    global frameCounter
    server.sendto(bytes(msg, 'utf-8'), addressDictionary[target])  # Convert to struct.pack later
    frameCounter += 1


while True:
    data, address = server.recvfrom(1024)
    if not data:
        break
    content = data.decode('utf-8')
    message = json.loads(content)
    frame = NetworkFrame(**message)

    if frame.m_SenderId not in addressDictionary:
        addressDictionary[frame.m_SenderId] = address

    # print(data.decode('utf-8'))
    if frame.m_Type == 2 and frame.m_SenderId == 'server' and frame.m_TargetId == 'proxy':
        # This is from the server to us to identify
        responseFrame = NetworkFrame(2, frameCounter, frame.m_SenderId, 'proxy', False)
        sendFrame(json.dumps(responseFrame), responseFrame.m_TargetId)
    elif frame.m_Type == 2 and frame.m_SenderId != 'server':
        # This is from a client but meant for the server, so lets send it to the server
        # We also send it back authorize frame but from us instead, to tell them they connected to the proxy
        if 'server' in addressDictionary:
            responseFrame = NetworkFrame(2, frameCounter, frame.m_SenderId, 'proxy', False)
            sendFrame(json.dumps(responseFrame), responseFrame.m_TargetId)
            sendFrame(content, frame.m_TargetId)
    else:
        sendFrame(content, frame.m_TargetId)

print("Closing proxy server, total sent frames: ", frameCounter)
server.close()
