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

namespace Client
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            Send();
            string str = (EncryptString(this.txbMessage.Text,key,iv));
            AddMessageEnc(this.txbUsername.Text + "> " + str);
            AddMessage(this.txbUsername.Text + "> " + this.txbMessage.Text);
            txbMessage.Clear();
        }

        IPEndPoint ep;
        Socket socketClient;
        static CngKey bobKey;
        static byte[] alicePubKeyBlob;
        static byte[] bobPubKeyBlob;

        static byte[] key;
        byte[] iv = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public void Connection()
        {
            if (this.txbIPServer.Text == string.Empty)
            {
                this.txbIPServer.Text = "127.0.0.1";
                ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);
            }
            else
                ep = new IPEndPoint(IPAddress.Parse(this.txbIPServer.Text), 9999);

            socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketClient.Connect(ep);

            do
            {
                socketClient.Send(bobPubKeyBlob);
            } while (this.txbUsername.Text == string.Empty);

            Thread listen = new Thread(Receive);
            listen.Start();

        }

        //Exit
        public void Exit()
        {
            socketClient.Close();
        }

        //Send Server
        public void Send()
        {
            if (this.txbMessage.Text != string.Empty)
                socketClient.Send(Serialize(EncryptString(this.txbMessage.Text, key, iv)));
        }

        //Receive
        public void Receive()
        {
            byte[] data = new byte[1024];
            socketClient.Receive(data);
            //string s = (string)Deserialize(data);
            alicePubKeyBlob = data;
            key = symKey();
            lbKey.Text ="Key: "+ Convert.ToBase64String(key);
            try
            {
                while (true)
                {
                    socketClient.Receive(data);

                    string mess = (string)Deserialize(data);
                    string msg = DecryptString(mess, key, iv);
                    if (mess == "false")
                    {
                        do
                        {
                            socketClient.Send(Serialize(Convert.ToBase64String(bobPubKeyBlob)));
                        } while (this.txbUsername.Text == string.Empty);
                    }
                    else
                    {
                        AddMessage("Alice> " + msg);
                        AddMessageEnc("Alice> " + mess);
                    }
                }

            }
            catch
            {
                Exit();
            }           
        }

        delegate void InfoMessageDel(String info);

        public void AddMessage(String info)
        {
            if (lbMess.InvokeRequired)
            {
                InfoMessageDel method = new InfoMessageDel(AddMessage);
                lbMess.Invoke(method, new object[] { info });
                return;
            }
            lbMess.Items.Add(info);
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

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Exit();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Connection();            
            this.btnConnect.Enabled = false;
            this.txbUsername.Enabled = false;
            this.txbIPServer.Enabled = false;
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            socketClient.Close();
            this.btnConnect.Enabled = true;
            this.txbIPServer.Enabled = true;
            this.txbUsername.Enabled = true;
        }

        public string EncryptString(string plainText, byte[] okey, byte[] iv)
        {
            // Instantiate a new Aes object to perform string symmetric encryption
            Aes encryptor = Aes.Create();

            encryptor.Mode = CipherMode.CBC;
            //encryptor.KeySize = 256;
            //encryptor.BlockSize = 128;
            encryptor.Padding = PaddingMode.Zeros;

            // Set key and IV
            encryptor.Key = okey;
            encryptor.IV = iv;

            // Instantiate a new MemoryStream object to contain the encrypted bytes
            MemoryStream memoryStream = new MemoryStream();

            // Instantiate a new encryptor from our Aes object
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

            // Return the encrypted data as a string
            return cipherText;
        }
        public string DecryptString(string cipherText, byte[] key, byte[] iv)
        {
            // Instantiate a new Aes object to perform string symmetric encryption
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

            // Instantiate a new CryptoStream object to process the data and write it to the 
            // memory stream
            CryptoStream cryptoStream = new CryptoStream(memoryStream, aesDecryptor, CryptoStreamMode.Write);

            // Will contain decrypted plaintext
            string plainText = String.Empty;

            try
            {
                // Convert the ciphertext string into a byte array
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
                // Close both the MemoryStream and the CryptoStream
                memoryStream.Close();
                cryptoStream.Close();
            }

            // Return the decrypted data as a string
            return plainText;
        }
        private static void CreateKeys()
        {
            bobKey = CngKey.Create(CngAlgorithm.ECDiffieHellmanP256);
            bobPubKeyBlob = bobKey.Export(CngKeyBlobFormat.EccPublicBlob);

        }
        public  byte[] symKey()
        {           
            byte[] symKey;
            using (var bobAlgorithm = new ECDiffieHellmanCng(bobKey))
            using (CngKey alicePubKey = CngKey.Import(alicePubKeyBlob, CngKeyBlobFormat.EccPublicBlob))
            {
                symKey = bobAlgorithm.DeriveKeyMaterial(alicePubKey);
            }
            return symKey;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            CreateKeys();
        }

    }
}
