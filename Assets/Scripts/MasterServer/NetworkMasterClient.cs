﻿using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class NetworkMasterClient : MonoBehaviour
{
	public bool dedicatedServer = true;
	public string MasterServerIpAddress = "127.0.0.1";
	public int MasterServerPort = 45555;
	public int updateRate = 60;
	public string gameTypeName = "Survival";
	public string gameName = "My Server";
	public int gamePort = 25560;

    public bool useGui = true;

	[SerializeField]
	public int yoffset = 0;

	string HostGameType = "";
	string HostGameName = "";

	MasterMsgTypes.Room[] hosts = null;

	public NetworkClient client = null;

	static NetworkMasterClient singleton;

	void Awake()
	{
		if (singleton == null)
		{
			singleton = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}
	public void InitializeClient()
	{
		if (client != null)
		{
			Debug.LogError("Already connected");
			return;
		}

		client = new NetworkClient();
		client.Connect(MasterServerIpAddress, MasterServerPort);

		// system msgs
		client.RegisterHandler(MsgType.Connect, OnClientConnect);
		client.RegisterHandler(MsgType.Disconnect, OnClientDisconnect);
		client.RegisterHandler(MsgType.Error, OnClientError);

		// application msgs
		client.RegisterHandler(MasterMsgTypes.RegisteredHostId, OnRegisteredHost);
		client.RegisterHandler(MasterMsgTypes.UnregisteredHostId, OnUnregisteredHost);
		client.RegisterHandler(MasterMsgTypes.ListOfHostsId, OnListOfHosts);
        //client.RegisterHandler(MasterMsgTypes.UpdatedHostId, )

		DontDestroyOnLoad(gameObject);
	}

	public void ResetClient()
	{
		if (client == null)
			return;

		client.Disconnect();
		client = null;
		hosts = null;
	}

	public bool isConnected
	{
		get
		{
			if (client == null) 
				return false;
			else 
				return client.isConnected;
		}
	}

	// --------------- System Handlers -----------------

	void OnClientConnect(NetworkMessage netMsg)
	{
		Debug.Log("Client Connected to Master");
	}

	void OnClientDisconnect(NetworkMessage netMsg)
	{
		Debug.Log("Client Disconnected from Master");
		ResetClient();
		OnFailedToConnectToMasterServer();
	}

	void OnClientError(NetworkMessage netMsg)
	{
		Debug.Log("ClientError from Master");
		OnFailedToConnectToMasterServer();
	}

	// --------------- Application Handlers -----------------

	void OnRegisteredHost(NetworkMessage netMsg)
	{
		var msg = netMsg.ReadMessage<MasterMsgTypes.RegisteredHostMessage>();
		OnServerEvent((MasterMsgTypes.NetworkMasterServerEvent)msg.resultCode);
	}

	void OnUnregisteredHost(NetworkMessage netMsg)
	{
		var msg = netMsg.ReadMessage<MasterMsgTypes.RegisteredHostMessage>();
		OnServerEvent((MasterMsgTypes.NetworkMasterServerEvent)msg.resultCode);
	}

	void OnListOfHosts(NetworkMessage netMsg)
	{
		var msg = netMsg.ReadMessage<MasterMsgTypes.ListOfHostsMessage>();
		hosts = msg.hosts;
		OnServerEvent(MasterMsgTypes.NetworkMasterServerEvent.HostListReceived);
	}


	public void ClearHostList()
	{
		if (!isConnected)
		{
			Debug.LogError("ClearHostList not connected");
			return;
		}
		hosts = null;

	}

	public MasterMsgTypes.Room[] PollHostList()
	{
		if (!isConnected)
		{
			Debug.LogError("PollHostList not connected");
			return null;
		}
		return hosts;
	}

	public void RegisterHost(string gameName, string comment, bool passwordProtected,int players, int playerLimit, int port)
	{
		if (!isConnected)
		{
			Debug.LogError("RegisterHost not connected");
			return;
		}

		var msg = new MasterMsgTypes.RegisterHostMessage();
		msg.gameTypeName = "Survival";
		msg.gameName = gameName;
		msg.comment = comment;
		msg.passwordProtected = passwordProtected;
        msg.players = players;
		msg.playerLimit = playerLimit;
		msg.hostPort = port;
		client.Send(MasterMsgTypes.RegisterHostId, msg);

		HostGameType = gameTypeName;
		HostGameName = gameName;

        StartCoroutine("HostUpdate");
	}

	public void RequestHostList(string gameTypeName)
	{
		if (!isConnected)
		{
			Debug.LogError("RequestHostList not connected");
			return;
		}

		var msg = new MasterMsgTypes.RequestHostListMessage();
		msg.gameTypeName = gameTypeName;
		client.Send(MasterMsgTypes.RequestListOfHostsId, msg);
	}

	public void UnregisterHost()
	{
		if (!isConnected)
		{
			Debug.LogError("UnregisterHost not connected");
			return;
		}

		var msg = new MasterMsgTypes.UnregisterHostMessage();
		msg.gameTypeName = HostGameType;
		msg.gameName = HostGameName;
		client.Send(MasterMsgTypes.UnregisterHostId, msg);
		HostGameType = "";
		HostGameName = "";

        StopCoroutine("HostUpdate");

        Debug.Log("send UnregisterHost");
	}

    //William Dewing 12/12/16
    //Sends the current player count to the server
    public void UpdateHost()
    {
        if (!isConnected)
        {
            Debug.LogError("UpdateHost not connected");
            return;
        }
        
        var msg = new MasterMsgTypes.UpdateHostMessage();
        msg.gameTypeName = HostGameType;
        msg.gameName = HostGameName;
        msg.players = NetworkManager.singleton.numPlayers;
        client.Send(MasterMsgTypes.UpdateHostId, msg);

        Debug.Log("send UpdateHost");
    }

	public virtual void OnFailedToConnectToMasterServer()
	{
		Debug.Log("OnFailedToConnectToMasterServer");
	}

	public virtual void OnServerEvent(MasterMsgTypes.NetworkMasterServerEvent evt)
	{
		Debug.Log("OnServerEvent " + evt);

		if (evt == MasterMsgTypes.NetworkMasterServerEvent.HostListReceived)
		{
			foreach (var h in hosts)
			{
				Debug.Log("Host:" + h.name + "addr:" + h.hostIp + ":" + h.hostPort);
			}
		}

		if (evt == MasterMsgTypes.NetworkMasterServerEvent.RegistrationSucceeded)
		{
			if (NetworkManager.singleton != null)
			{
				NetworkManager.singleton.StartHost();
			}
		}

		if (evt == MasterMsgTypes.NetworkMasterServerEvent.UnregistrationSucceeded)
		{
			if (NetworkManager.singleton != null)
			{
				NetworkManager.singleton.StopHost();
			}
		}
	}

    public void DisconectClient()
    {
        ResetClient();
		if (NetworkManager.singleton != null)
	    {
			NetworkManager.singleton.StopServer();
			NetworkManager.singleton.StopClient();
        }
		HostGameType = "";
		HostGameName = "";
    }

    IEnumerator HostUpdate()
    {
        Debug.Log("Host autoupdate started");
        while (isConnected)
        {
            UpdateHost();
            
            yield return new WaitForSeconds(updateRate);
        }
        Debug.Log("Host autoupdate stopped");
    }

    void OnGUI()
    {
        if (!useGui)
        {
            return;
        }

        if (client != null && client.isConnected)
        {
            if (GUI.Button(new Rect(100, 20 + yoffset, 200, 20), "MasterClient Disconnect"))
            {

            }
        }
        else
        {
            if (GUI.Button(new Rect(100, 20 + yoffset, 200, 20), "MasterClient Connect"))
            {
                InitializeClient();
            }
            return;
        }


        if (HostGameType == "")
        {
            GUI.Label(new Rect(100, 50 + yoffset, 80, 20), "GameType:");
            gameTypeName = GUI.TextField(new Rect(180, 50 + yoffset, 200, 20), gameTypeName);

            GUI.Label(new Rect(100, 70 + yoffset, 80, 20), "GameName:");
            gameName = GUI.TextField(new Rect(180, 70 + yoffset, 200, 20), gameName);

            if (GUI.Button(new Rect(100, 90 + yoffset, 200, 20), "RegisterHost"))
            {
                int port = gamePort;
                if (NetworkManager.singleton != null)
                {
                    port = NetworkManager.singleton.networkPort;
                }
                RegisterHost(gameName, "none", false, 0, 8, port);
            }

            if (GUI.Button(new Rect(100, 120 + yoffset, 200, 20), "List Hosts"))
            {
                RequestHostList(gameTypeName);
            }
        }
        else
        {
            if (GUI.Button(new Rect(100, 120 + yoffset, 120, 20), "UnregisterHost"))
            {
                UnregisterHost();
            }
        }

        if (hosts != null)
        {
            int y = 140;
            foreach (var h in hosts)
            {
                if (GUI.Button(new Rect(120, y + yoffset, 240, 20), "Host:" + h.name + "addr:" + h.hostIp + ":" + h.hostPort))
                {
                    if (NetworkManager.singleton != null)
                    {
                        NetworkManager.singleton.networkAddress = h.hostIp;
                        NetworkManager.singleton.networkPort = h.hostPort;
                        NetworkManager.singleton.StartClient();
                    }
                }
                y += 22;
            }
        }
    }
}
