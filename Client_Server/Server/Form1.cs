using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Server
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }


        IPEndPoint ep;
        Socket socketServer;
        List<Socket> socketClientList;

        //Diffie Hellman Key Exchange Alice = Server, Bob = Client
        static CngKey aliceKey;
        static byte[] alicePubKeyBlob;
        static byte[] bobPubKeyBlob;

        static byte[] key;
        byte[] iv = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
     
        private void btnSend_Click(object sender, EventArgs e)
        {
            foreach (Socket item in socketClientList)
            {
                Send(item);
            }
            string str = (EncryptString(this.txbMessage.Text, key, iv));
            AddMessage(this.txbUser.Text + "> " + this.txbMessage.Text);
            AddMessageEnc(this.txbUser.Text + "> " + str);
            txbMessage.Clear();          
        }
        //Connect to client
        public void Connection()
        {
            socketClientList = new List<Socket>();

            if (this.txtIPAddress.Text == string.Empty)
            {
                this.txtIPAddress.Text = "127.0.0.1";
                ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);
            }      
            else
                ep = new IPEndPoint(IPAddress.Parse(this.txtIPAddress.Text), 9999);
            socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketServer.Bind(ep);
            AddMessage("Waiting for connection!!!!");
            AddMessageEnc("Waiting for connection!!!!");
                       
            Thread listen = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        socketServer.Listen(1);
                        Socket client = socketServer.Accept();//luu thang da ket noi
                        socketClientList.Add(client); // them vao danh sach client
                      
                        Thread listenReceive = new Thread(Receive);
                        listenReceive.Start(client);//lang nghe tu client
                    }
                }
                catch
                {                   
                    ep = new IPEndPoint(IPAddress.Any, 9999);
                    socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                }
            });

            listen.Start();
        }

        //Send Client
        public void Send(Socket client)
        {           
            if (this.txbMessage.Text != string.Empty)
                client.Send(Serialize( EncryptString(this.txbMessage.Text,key,iv)));
        }

        //Receive
        public void Receive(object obj)
        {
            //Receive BobPublicKey
            Socket client = obj as Socket;
            byte[] data = new byte[1024];
            client.Send(alicePubKeyBlob);
            client.Receive(data);
            bobPubKeyBlob = data;
            
            key = symKey();
            lbKey.Text = "Key: "+ Convert.ToBase64String(key);
            AddMessage("Bob connected!!!");
            AddMessageEnc("Bob connected!!!");
            try
            {
                while (true)
                {
                    client.Receive(data);                    
                    string mess = (string)Deserialize(data);
                    string msg = DecryptString(mess, key, iv);
                    if (mess == "false")
                    {
                        do
                        {
                            client.Send(Serialize(Convert.ToBase64String(alicePubKeyBlob)));
                        } while (this.txbUser.Text == string.Empty);
                    }
                    else
                    {
                        AddMessage("Bob> " + msg);
                        AddMessageEnc("Bob> " + mess);
                   }                                        
                }                                
            }
            catch
            {
                AddMessage("Bob disconnect!!!!");
                AddMessageEnc(" Bob disconnect!!!!");
                socketClientList.Remove(client);
                client.Close();
            }
        }

        delegate void InfoMessageDel(String msg);
        public void AddMessage(string msg)
        {
            if (lbMess.InvokeRequired )
            {
                InfoMessageDel method = new InfoMessageDel(AddMessage);
                lbMess.Invoke(method, new object[] { msg });
                return;
            }
            lbMess.Items.Add(msg);
        }
        public void AddMessageEnc(String info)
        {
            if (lbMessEnc.InvokeRequired)
            {
                InfoMessageDel method = new InfoMessageDel(AddMessageEnc);
                lbMessEnc.Invoke(method, new object[] { info });
                return;
            }
            lbMessEnc.Items.Add(info);
        }

        public byte[] Serialize(object obj)
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, obj);
            return stream.ToArray();
        }
                
        public object Deserialize(byte[] data)
        {
            MemoryStream stream = new MemoryStream(data);
            BinaryFormatter formatter = new BinaryFormatter();
            return formatter.Deserialize(stream);

        }
    
        private void btnListen_Click(object sender, EventArgs e)
        {
            Connection();
            this.btnListen.Enabled = false;
            this.txtIPAddress.Enabled = false;
            this.txbUser.Enabled = false;

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(socketServer !=null)
                socketServer.Close();
        }
        public string EncryptString(string plainText, byte[] key, byte[] iv)
        {
            Aes encryptor = Aes.Create();
            encryptor.Mode = CipherMode.CBC;
            //encryptor.KeySize = 256;
            //encryptor.BlockSize = 128;
            encryptor.Padding = PaddingMode.Zeros;
            // Set key and IV
            encryptor.Key = key;
            encryptor.IV = iv;

            MemoryStream memoryStream = new MemoryStream();
            ICryptoTransform aesEncryptor = encryptor.CreateEncryptor();
            // Instantiate a new CryptoStream object to process the data and write it to the 
            // memory stream
            CryptoStream cryptoStream = new CryptoStream(memoryStream, aesEncryptor, CryptoStreamMode.Write);

            // Convert the plainText string into a byte array
            byte[] plainBytes = Encoding.ASCII.GetBytes(plainText);
            // Encrypt the input plaintext string
            cryptoStream.Write(plainBytes, 0, plainBytes.Length);
            // Complete the encryption process
            cryptoStream.FlushFinalBlock();

            // Convert the encrypted data from a MemoryStream to a byte array
            byte[] cipherBytes = memoryStream.ToArray();
            // Close both the MemoryStream and the CryptoStream
            memoryStream.Close();
            cryptoStream.Close();
            // Convert the encrypted byte array to a base64 encoded string
            string cipherText = Convert.ToBase64String(cipherBytes, 0, cipherBytes.Length);

            return cipherText;
        }

        public string DecryptString(string cipherText, byte[] key, byte[] iv)
        {
            Aes encryptor = Aes.Create();

            encryptor.Mode = CipherMode.CBC;
            //encryptor.KeySize = 256;
            //encryptor.BlockSize = 128;
            encryptor.Padding = PaddingMode.Zeros;
            // Set key and IV
            encryptor.Key = key;
            encryptor.IV = iv;

            // Instantiate a new MemoryStream object to contain the encrypted bytes
            MemoryStream memoryStream = new MemoryStream();
            // Instantiate a new encryptor from our Aes object
            ICryptoTransform aesDecryptor = encryptor.CreateDecryptor();
            CryptoStream cryptoStream = new CryptoStream(memoryStream, aesDecryptor, CryptoStreamMode.Write);

            string plainText = String.Empty;
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                // Decrypt the input ciphertext string
                cryptoStream.Write(cipherBytes, 0, cipherBytes.Length);
                // Complete the decryption process
                cryptoStream.FlushFinalBlock();

                // Convert the decrypted data from a MemoryStream to a byte array
                byte[] plainBytes = memoryStream.ToArray();
                // Convert the decrypted byte array to string
                plainText = Encoding.ASCII.GetString(plainBytes, 0, plainBytes.Length);
            }
            finally
            {
                memoryStream.Close();
                cryptoStream.Close();
            }
            // Return the decrypted data as a string
            return plainText;
        }

        public static void CreateKeys()
        {
            aliceKey = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256);
            alicePubKeyBlob = aliceKey.Export(CngKeyBlobFormat.EccPublicBlob);
        }
        public  byte[] symKey()
        {
            byte[] symKey;
            using (var aliceAlgorithm = new ECDiffieHellmanCng(aliceKey))
            using (CngKey bobPubKey = CngKey.Import(bobPubKeyBlob, CngKeyBlobFormat.EccPublicBlob))
            {
                symKey = aliceAlgorithm.DeriveKeyMaterial(bobPubKey);
            }
            return symKey;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CreateKeys();
        }

    }
}
