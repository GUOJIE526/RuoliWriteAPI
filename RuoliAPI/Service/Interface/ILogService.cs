﻿namespace RuoliAPI.Services.Interfaces
{
    public interface ILogService
    {
        Task LogAsync(Guid userId, string action, string message, string ip);
    }
}
