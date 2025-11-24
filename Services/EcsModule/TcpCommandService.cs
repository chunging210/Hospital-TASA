using TASA.Program;

namespace TASA.Services.EcsModule
{
    public class TcpCommandService : IService
    {
        /// <summary>
        /// 發送 TCP 指令到環控設備
        /// </summary>
        public static async Task SendTcpCommand(string host, int port, string macro)
        {
            const int TimeoutMilliseconds = 5000;
            var connectionTimeout = TimeSpan.FromMilliseconds(TimeoutMilliseconds);
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(host, port).WaitAsync(connectionTimeout);

            await using var stream = client.GetStream();
            string commandToSend = macro + "\r\n";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(commandToSend);

            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        //public static void SendTcpCommand(string host, int port, string macro)
        //{
        //    using var client = new System.Net.Sockets.TcpClient();

        //    var connectTask = client.ConnectAsync(host, port);
        //    if (!connectTask.Wait(TimeSpan.FromSeconds(5)) || !client.Connected)
        //    {
        //        return;
        //    }

        //    using var stream = client.GetStream();
        //    stream.ReadTimeout = 5000;
        //    stream.WriteTimeout = 5000;

        //    string commandToSend = macro + "\r\n";
        //    byte[] data = System.Text.Encoding.UTF8.GetBytes(commandToSend);

        //    stream.Write(data, 0, data.Length);
        //    stream.Flush();
        //}
    }
}
