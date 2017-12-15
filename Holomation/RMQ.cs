using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Diagnostics;
using System.Text;

namespace Holo1
{
    class RMQ
    {
        private string datas = "";
        private string routingkey = "";

        public ConnectionFactory connectionFactory;
        public IConnection connection;
        public IModel channelPengirim;
        public IModel channelPenerima;

        public void InitRMQConnection(string host = "167.205.7.226", int port = 5672, string user = "ARmachine",
        string pass = "12345", string vhost = "/ARX")
        {
            connectionFactory = new ConnectionFactory();
            connectionFactory.HostName = host;
            connectionFactory.Port = port;
            connectionFactory.UserName = user;
            connectionFactory.Password = pass;
            connectionFactory.VirtualHost = vhost;
        }

        internal void CreateRMQChannel(string queue_name = "TMDG2017-5-Fertra-Pengirim")
        {
            if (connection.IsOpen)
            {
                channelPengirim = connection.CreateModel();
                //Debug.WriteLine("Channel " + (channel.IsOpen ? "Berhasil!" : "Gagal!"));
            }
            if (channelPengirim.IsOpen)
            {
                channelPengirim.QueueDeclare(queue: queue_name,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);
                // Debug.WriteLine("Queue telah dideklarasikan..");
            }
        }

        public void CreateRMQConnection()
        {
            connection = connectionFactory.CreateConnection();
            Debug.WriteLine("Koneksi " + (connection.IsOpen ? "Berhasil!" : "Gagal!"));
        }

        public void SendMessage(string tujuan, string msg = "send")
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(msg);// konversi pesan dalam bentuk string menjadi byte
            channelPengirim.BasicPublish(exchange: "amq.topic",
            routingKey: tujuan,
            basicProperties: null,
            body: responseBytes);
            //Debug.WriteLine("Pesan: '" + msg + "' telah dikirim.");
        }

        public void WaitingMessage(string queue_name = "TMDG2017-5-Fertra-Penerima")
        {
            using (channelPenerima = connection.CreateModel())
            {
                channelPenerima.QueueDeclare(queue: queue_name,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

                //subscribe ke topic atau routing key tertentu
                channelPenerima.QueueBind(queue_name, "amq.topic", "door.action", null);

                var consumer = new EventingBasicConsumer(channelPenerima);
                consumer.Received += (model, ea) =>
                {
                    //get data message
                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body);

                    //get nama topic alias routing key
                    var topic = ea.RoutingKey;

                    datas = message;
                    routingkey = topic;

                    //Debug.WriteLine(" [x] Pesan diterima: {0}", message + " pada " + topic);
                };
                channelPenerima.BasicConsume(queue: queue_name, noAck: true, consumer: consumer);
            }
        }

        public string getDataTopic()
        {
            return routingkey;
        }

        public string getDataMessage()
        {
            return datas;
        }
    }
}