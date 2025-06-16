using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RuoliAPI.Models;
using System.Net.Http.Headers;
using System.Text;

namespace RuoliAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly CalligraphyContext _context;
        private readonly string _channelAccessToken;

        public WebhookController(IHttpClientFactory clientFactory, IConfiguration configuration, CalligraphyContext context)
        {
            _clientFactory = clientFactory;
            _context = context;
            _channelAccessToken = configuration["LineToken:ChannelAccessToken"] ?? string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] dynamic body)
        {
            foreach (var evt in body.events)
            {
                string type = evt.type;
                if (type == "follow")
                {
                    string replyToken = evt.replyToken;
                    await ReplyOpenNotifyButton(replyToken);
                }
                else if (type == "message")
                {
                    string text = evt.message.text;
                    string lineUserId = evt.source.userId;
                    string replyToken = evt.replyToken;

                    if (text == "開啟通知")
                    {
                        await EnableNotify(lineUserId);
                        await ReplyText(replyToken, "已開啟通知");
                    }
                    else if (text == "關閉通知")
                    {
                        await DisableNotify(lineUserId);
                        await ReplyText(replyToken, "已關閉通知");
                    }
                }
            }
            return Ok();
        }

        private async Task ReplyOpenNotifyButton(string replyToken)
        {
            var payload = new
            {
                replyToken,
                messages = new[]
                {
                    new
                    {
                        type = "template",
                        altText = "開啟通知",
                        template = new
                        {
                            type = "confirm",
                            text = "要開啟通知嗎？",
                            actions = new[]
                            {
                                new { type = "message", label = "開啟通知", text = "開啟通知" }
                            }
                        }
                    }
                }
            };
            await PostToLine("https://api.line.me/v2/bot/message/reply", payload);
        }

        private async Task ReplyText(string replyToken, string text)
        {
            var payload = new
            {
                replyToken,
                messages = new[]
                {
                    new { type = "text", text }
                }
            };
            await PostToLine("https://api.line.me/v2/bot/message/reply", payload);
        }

        private async Task EnableNotify(string lineUserId)
        {
            var duplicates = _context.TbExhLine.Where(l => l.LineUserId == lineUserId);
            _context.TbExhLine.RemoveRange(duplicates);
            var target = _context.TbExhLine.OrderByDescending(l => l.CreateDate).FirstOrDefault(l => l.LineUserId == null);
            if (target != null)
            {
                target.LineUserId = lineUserId;
            }
            else
            {
                _context.TbExhLine.Add(new TbExhLine { LineUserId = lineUserId, UserId = Guid.Empty });
            }
            await _context.SaveChangesAsync();
        }

        private async Task DisableNotify(string lineUserId)
        {
            var targets = _context.TbExhLine.Where(l => l.LineUserId == lineUserId);
            foreach (var t in targets)
            {
                t.LineUserId = null;
            }
            await _context.SaveChangesAsync();
        }

        private async Task PostToLine(string url, object payload)
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }
    }
}
