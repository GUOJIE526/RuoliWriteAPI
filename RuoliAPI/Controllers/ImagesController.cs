using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using RuoliAPI.Models;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace RuoliAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : Controller
    {
        private readonly CalligraphyContext _context;
        private readonly IHttpClientFactory _clientFactory;
        private readonly string _channelAccessToken;

        public ImagesController(CalligraphyContext context, IHttpClientFactory clientFactory, IConfiguration configuration)
        {
            _context = context;
            _clientFactory = clientFactory;
            _channelAccessToken = configuration["LineToken:ChannelAccessToken"] ?? string.Empty;
        }

        //GET api/images
        [HttpGet(Name = "GetImage")]
        public async Task<IActionResult> GetImage()
        {
            //讀取所有isVisivble是true的圖片
            var imgs = await _context.TbExhArtwork
                .AsNoTracking()
                .Where(i => i.IsVisible)
                .Select(i => new
                {
                    i.ArtworkId,
                    i.Title,
                    i.CreatedYear,
                    i.ImageUrl,
                    i.Description,
                    i.Dimensions,
                    i.Material
                }).OrderByDescending(i => i.CreatedYear)
                .ToListAsync();

            if (imgs == null || imgs.Count == 0)
            {
                return NotFound("No images found.");
            }
            return Ok(imgs);
        }

        //回傳該作品的讚數
        [HttpGet("{artworkId}/likes", Name = "GetArtworkLikes")]
        public async Task<IActionResult> GetArtworkLikes(Guid artworkId)
        {
            var likeCount = await _context.TbExhLike
                .AsNoTracking()
                .CountAsync(l => l.ArtworkId == artworkId);
            return Ok(new { ArtworkId = artworkId, LikeCount = likeCount });
        }

        //新增讚數
        [HttpPost("{artworkId}/likes", Name = "AddArtworkLike")]
        public async Task<IActionResult> AddArtworkLike(Guid artworkId)
        {
            var artwork = await _context.TbExhArtwork.FindAsync(artworkId);
            if (artwork == null)
            {
                return NotFound("Artwork not found.");
            }

            //同一個ip一天只能按一次讚
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var existingLike = await _context.TbExhLike
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.ArtworkId == artworkId && l.IpAddress == ip && l.CreateDate.Date == DateTimeOffset.Now.Date);
            if (existingLike == null)
            {
                var like = new TbExhLike
                {
                    ArtworkId = artworkId,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                    CreateFrom = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "",
                    Creator = HttpContext.Connection.RemoteIpAddress?.ToString() ?? ""
                };
                _context.TbExhLike.Add(like);
                try
                {
                    await _context.SaveChangesAsync();

                    await NotifyAuthorAsync(artwork);

                    return Ok();
                }
                catch (DbUpdateException ex)
                {
                    //處理資料庫更新異常
                    return BadRequest($"Error adding like: {ex.Message}");
                }
            }
            else
            {
                return Ok("今日已按過讚");
            }
        }

        private async Task NotifyAuthorAsync(TbExhArtwork artwork)
        {
            if (artwork.Writer == null)
            {
                return;
            }

            var lineIds = await _context.TbExhLine
                .Where(l => l.UserId == artwork.Writer && l.LineUserId != null)
                .Select(l => l.LineUserId!)
                .ToListAsync();

            foreach (var lineId in lineIds)
            {
                var payload = new
                {
                    to = lineId,
                    messages = new[]
                    {
                        new { type = "text", text = $"您的 {artwork.Title} 已收到一個新讚!" }
                    }
                };

                var client = _clientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _channelAccessToken);
                var json = JsonConvert.SerializeObject(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                await client.PostAsync("https://api.line.me/v2/bot/message/push", content);
            }
        }
    }
}
