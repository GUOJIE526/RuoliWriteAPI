using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RuoliAPI.Models;
using System.Text;

namespace RuoliAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LineBotController : ControllerBase
    {
        private readonly string _channelAccessToken;
        private readonly CalligraphyContext _context;
        public LineBotController(IConfiguration configuration, CalligraphyContext context)
        {
            _channelAccessToken = configuration["LineToken:ChannelAccessToken"] ?? string.Empty;
            _context = context;
        }
        // POST api/linebot
        [HttpPost]
        public IActionResult Post()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var bot = new isRock.LineBot.Bot(_channelAccessToken);
                var postData = reader.ReadToEndAsync().Result;
                var rcvMsg = isRock.LineBot.Utility.Parsing(postData);
                if (rcvMsg != null && rcvMsg.events != null && rcvMsg.events.Count > 0)
                {
                    if (rcvMsg.events[0].type.ToLower() == "follow")
                    {
                        var userInfo = bot.GetUserInfo(rcvMsg.events[0].source.userId);
                        var lineUser = new TbExhLine
                        {
                            LineUserId = rcvMsg.events[0].source.userId,
                            CreateFrom = HttpContext.Connection.RemoteIpAddress?.ToString() ?? null,
                            Creator = userInfo.displayName ?? null,
                        };
                        // 檢查是否已存在該用戶
                        var existingUser = _context.TbExhLine
                            .FirstOrDefault(u => u.LineUserId == lineUser.LineUserId);
                        if (existingUser != null)
                        {
                            _context.TbExhLine.Remove(existingUser);
                        }
                        _context.TbExhLine.Add(lineUser);
                        _context.SaveChanges();
                    }
                }
            }
            return Ok();
        }
    }
}
