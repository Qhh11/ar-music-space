# AR-Music-Space

A Unity-based augmented reality music playground for Android, combining virtual instrument interaction with a separate hand-mesh server.

## At a glance

- **Stack:** Unity 6, C#, and mobile AR.
- **Interaction:** instrument controls and hand–instrument interaction scripts.
- **Runtime:** Android client plus an external hand-mesh server.
- **Editor version:** `6000.1.9f1`, as recorded in `ProjectSettings/ProjectVersion.txt`.

The server is a separate dependency and is not included in this repository. See the original setup instructions below. Hardware and server integration have not been retested during migration.

Originally developed in the [LRZ GitLab repository](https://gitlab.lrz.de/00000000014B3F3B/ar-music-space). Original contributor attribution and bundled license notices are retained.

#### Installation

Unity: version 6000.1.9f1/6000.1.10f1

hand-mesh-server: see hand-mesh-server[[Jiong Liu / hand-mesh-server · GitLab](https://gitlab.lrz.de/liujio/hand-mesh-server)]. We recommend to run the server on wsl if on Windows system.

You would require a Android device to run.

#### How to run

First, run the PowerShell under administration mode. Run the following command:

```bash
wsl hostname -I
# You will get a IP address, let's call it <wslhostname>
netsh interface portproxy add v4tov4 listenport=5000 listenaddress=0.0.0.0 connectport=5000 connectaddress=<wslhostname>
netsh advfirewall firewall add rule name="WSL Port 5000" dir=in action=allow protocol=TCP localport=5000
```

After that, start the hand-mesh-server[[Jiong Liu / hand-mesh-server · GitLab](https://gitlab.lrz.de/liujio/hand-mesh-server)] on wsl.

Then, go to Unity and find the **Main Camera** under **ARF XR Origin Set Up/XR Origin/CameraOffset**. There is a script called **client**. Modify the **Server URL** as following: 

```bash
http://<your-ipv4-address>:5000
# You can find your ipv4 address using ipconfig
```

Finally, build and run it with a connected Android device and enjoy the game.
