using Calligraphy.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly IClientIpService _getIP;

        public LineBotController(IConfiguration configuration, CalligraphyContext context, IClientIpService getIP)
        {
            _channelAccessToken = configuration["LineToken:ChannelAccessToken"] ?? string.Empty;
            _context = context;
            _getIP = getIP;
        }
        // POST api/linebot
        [HttpPost]
        public async Task<IActionResult> Post()
        {
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var bot = new isRock.LineBot.Bot(_channelAccessToken);
                var postData = reader.ReadToEndAsync().Result;
                var rcvMsg = isRock.LineBot.Utility.Parsing(postData);
                if (rcvMsg.events != null && rcvMsg.events.Any())
                {
                    foreach (var ev in rcvMsg.events)
                    {
                        switch (ev.type.ToLower())
                        {
                            case "follow":
                                var userInfo = bot.GetUserInfo(ev.source.userId);
                                var lineUser = new TbExhLine
                                {
                                    LineUserId = ev.source.userId,
                                    Unfollow = false,
                                    CreateFrom = _getIP.GetClientIP() ?? null,
                                    Creator = userInfo.displayName ?? null,
                                };
                                // 檢查是否已存在該用戶
                                var existingUser = await _context.TbExhLine
                                    .FirstOrDefaultAsync(u => u.LineUserId == lineUser.LineUserId);
                                if (existingUser != null)
                                {
                                    _context.TbExhLine.Remove(existingUser);
                                }
                                _context.TbExhLine.Add(lineUser);
                                await _context.SaveChangesAsync();
                                break;
                            case "unfollow":
                                //TODO: 處理取消關注事件
                                var unfollowUserId = ev.source.userId;
                                var unfollowUser = await _context.TbExhLine
                                    .FirstOrDefaultAsync(u => u.LineUserId == unfollowUserId);
                                if (unfollowUser != null)
                                {
                                    unfollowUser.Unfollow = true;
                                    _context.TbExhLine.Update(unfollowUser);
                                    await _context.SaveChangesAsync();
                                }
                                break;
                            case "message":
                                //TODO: 處理訊息事件
                                var messageEventUserId = ev.source.userId;
                                var messageEventUser = await _context.TbExhLine
                                    .FirstOrDefaultAsync(u => u.LineUserId == messageEventUserId);
                                string reply = string.Empty;
                                if (messageEventUser != null)
                                {
                                    if (ev.message.text == "開啟通知")
                                    {
                                        messageEventUser.Notify = true;
                                        messageEventUser.Modifier = bot.GetUserInfo(messageEventUserId).displayName ?? null;
                                        messageEventUser.ModifyFrom = _getIP.GetClientIP() ?? null;
                                        _context.TbExhLine.Update(messageEventUser);
                                        await _context.SaveChangesAsync();
                                        reply = "已為您開啟通知。";
                                    }
                                    else if (ev.message.text == "關閉通知")
                                    {
                                        messageEventUser.Notify = false;
                                        messageEventUser.Modifier = bot.GetUserInfo(messageEventUserId).displayName ?? null;
                                        messageEventUser.ModifyFrom = _getIP.GetClientIP() ?? null;
                                        _context.TbExhLine.Update(messageEventUser);
                                        await _context.SaveChangesAsync();
                                        reply = "已為您關閉通知。";
                                    }
                                    else
                                    {
                                        reply = "感謝您的訊息！\r\n\r\n很抱歉，本帳號無法個別回覆用戶的訊息。\r\n\r\n如需要開啟通知，請回覆 \"開啟通知\"\r\n如需要關閉通知，請回覆 \"關閉通知\"\r\n\r\n敬請期待我們下次發送的內容喔~";
                                    }
                                    if (!string.IsNullOrEmpty(reply))
                                    {
                                        bot.ReplyMessage(ev.replyToken, reply);
                                    }
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
                return Ok();
            }
        }
    }
}
