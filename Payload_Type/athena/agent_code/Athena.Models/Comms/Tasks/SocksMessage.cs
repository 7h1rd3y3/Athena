namespace Athena.Models.Mythic.Response
{
    [Serializable]
    public class SocksMessage
    {
        public bool exit { get; set; }
        public int server_id { get; set; }
        public string data { get; set; }
    }
}
