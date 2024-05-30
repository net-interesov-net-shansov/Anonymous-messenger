// using System;
// using System.Text;
// using System.Collections.Generic;
// using System.Net;
// using System.Net.Sockets;
// using System.Threading;
// using Microsoft.AspNetCore.Http.Connections;

// namespace TcpChatServer
// {
//     class TcpChatServer
//     {
//         // Клиент
//         private TcpListener _listener;
    
//         // Определение двух типов клиентов
//         private List<TcpClient> _viewers = new List<TcpClient>();
//         private List<TcpClient> _messengers = new List<TcpClient>();

//         // Занятые имена
//         private Dictionary<TcpClient, string> _names = new Dictionary<TcpClient, string>();

//         // Сообщения, ожидающие отправки
//         private Queue<string> _messageQueue = new Queue<string>();

//         // Прочие данные
//         public readonly string ChatName;
//         public readonly int Port;
//         public bool Running { get; private set; }

//         // Размер буффера
//         public readonly int BufferSize = 2 * 1024; 

//         // Создание TCP сервера с заданными параметрами
//         public TcpChatServer(string chatName, int port)
//         {
//             ChatName = chatName;
//             Port = port;
//             Running = false;

//             // Клиент может подключиться с любого IP
//             _listener = new TcpListener(IPAddress.Any, Port);
//         }
//         public void Shutdown()
//         {
//             Running = false;
//             Console.WriteLine("Shutting down the server");
//         }

//         // Запуск сервера. При вызове Shutdown() сервер отключается
//         public void Run()
//         {
//             Console.WriteLine("Starting the \"{0}\" TCP Chat Server on port {1}", ChatName, Port);

//             // Непосредственно, запуск сервера
//             _listener.Start();
//             Running = true;

//             // Главный цикл
//             while(Running)
//             {
//                 // Проверка наличия новых клиентов
//                 if (_listener.Pending())
//                     _handleNewConnection();

//                 // Проверка остальных событий
//                 _checkForDisconnects();
//                 _checkForNewMessages();
//                 _sendMessages();

//                 // Остановка на 10 мс
//                 Thread.Sleep(10);
//             }

//             // Процесс остановки сервера, удаление всех клиентов
//             foreach (TcpClient v in _viewers)
//                 _clearupClient(v);
//             foreach (TcpClient m in _messengers)
//                 _clearupClient(m);
//             _listener.Stop();

//             Console.WriteLine("Server is shut down");
//         }

//         private void _handleNewConnection()
//         {
//             // Устанавливается, чтобы у следующего клиенты была возможность корректно подключиться
//             bool good = false;

//             // Инициализация нового клиента
//             TcpClient newClient = _listener.AcceptTcpClient();
//             NetworkStream stream = newClient.GetStream();

//             newClient.SendBufferSize = BufferSize;
//             newClient.ReceiveBufferSize = BufferSize;

//             EndPoint endPoint = newClient.Client.RemoteEndPoint;
//             Console.WriteLine("New connection from {0}", endPoint);

//             // Установка размера буффера для сообщений
//             byte[] msgBuffer = new byte[BufferSize];
//             // Заполнение буффера сообщения
//             int bytesRead = stream.Read(msgBuffer, 0, msgBuffer.Length);
            
//             // Если в буффере что-то есть...
//             if (bytesRead > 0)
//             {
//                 // Декодируем сообщение в string
//                 string msg = Encoding.UTF8.GetString(msgBuffer, 0, bytesRead);
                
//                 // Если получен код "наблюдатель"...
//                 if (msg == "viewer")
//                 {
//                     // Меняем флаг
//                     good = true;
//                     _viewers.Add(newClient);

//                     Console.WriteLine("{0} viewers", endPoint);

//                     // ОТправка приветственного сообщения
//                     msg = String.Format("Welcome to the \"{0}\" Chat Server!", ChatName);
//                     msgBuffer = Encoding.UTF8.GetBytes(msg);
//                     stream.Write(msgBuffer, 0, msgBuffer.Length);
//                 }
//                 // Если получено имя пользователя...
//                 else if (msg.StartsWith("name:"))
//                 {
//                     // Получаем имя пользователя
//                     string name = msg.Substring(msg.IndexOf(':') + 1);

//                     // Если имя уникально...
//                     if ((name != string.Empty) && (!_names.ContainsValue(name)))
//                     {
//                         // Меняем флаг
//                         good = true;
//                         _names.Add(newClient, name);
//                         _messengers.Add(newClient);

//                         Console.WriteLine("{0} is a Messenger with the name {1}.", endPoint, name);

//                         // Добавляем в очередь уведомление о новом пользователе
//                         _messageQueue.Enqueue(String.Format("A new Messenger has joined the chat: {0}", name));
//                     }
//                 }
//                 // Во всех остальных случаях разрываем соединение
//                 else
//                 {
//                     Console.WriteLine("{0} - is not identified", endPoint);
//                     _clearupClient(newClient);
//                 }
//             }

//             if (!good)
//                 _clearupClient(newClient);            
//         }

//         // Проверка отключившихся пользователей
//         private void _checkForDisconnects()
//         {
//             // Проверка отключившихся наблюдателей
//             foreach (TcpClient v in _viewers.ToArray())
//             {
//                 if (_isDisconnected(v))
//                 {
//                     Console.WriteLine("Viewer {0} has left", v.Client.RemoteEndPoint);
//                     _viewers.Remove(v);
//                     _clearupClient(v);
//                 }
//             }

//             // Проверка отключившихся отправителей
//             foreach (TcpClient m in _messengers.ToArray())
//             {
//                 if (_isDisconnected(m))
//                 {
//                     Console.WriteLine("Messenger {0} has left", m.Client.RemoteEndPoint);
//                     _viewers.Remove(m);
//                     _clearupClient(m);
//                 }
//             }
//         }

//         private void _checkForNewMessages()
//         {
//             foreach (TcpClient m in _messengers)
//             {
//                 int messageLength = m.Available;
//                 if (messageLength > 0)
//                 {
//                     byte[] msgBuffer = new byte[messageLength];
//                     m.GetStream().Read(msgBuffer, 0, messageLength);

//                     string msg = String.Format("{0}: {1}", _names[m], Encoding.UTF8.GetString(msgBuffer));
//                     _messageQueue.Enqueue(msg);                    
//                 }
//             }
//         }

//         private void _sendMessages()
//         {
//             foreach (string msg in _messageQueue)
//             {
//                 byte[] msgBuffer = Encoding.UTF8.GetBytes(msg);

//                 foreach (TcpClient v in _viewers)
//                     v.GetStream().Write(msgBuffer, 0, msgBuffer.Length);    // Blocks
//             }
//             _messageQueue.Clear();
//         }

//         // Проверка отключившихся сокетов
//         // Источник -- http://stackoverflow.com/questions/722240/instantly-detect-client-disconnection-from-server-socket
//         private static bool _isDisconnected(TcpClient client)
//         {
//             try
//             {
//                 return client.Client.Poll(10 * 1000, SelectMode.SelectRead) && (client.Available == 0);
//             }
//             catch (SocketException se)
//             {
//                 return true;
//             }
//         }
//         private static void _clearupClient(TcpClient client)
//         {
//             client.GetStream().Close();
//             client.Close(); 
//         }
//         public static TcpChatServer chat;

//         protected static void InterruptHandler(object sender, ConsoleCancelEventArgs args)
//         {
//             chat.Shutdown();
//             args.Cancel = true;
//         }
//         public static void Main()
//         {
//             string name = "Bad IRC";
//             int port = 6000;
//             chat = new TcpChatServer(name, port);

//             Console.CancelKeyPress += InterruptHandler;
//             chat.Run();
//         }
//     }
// }