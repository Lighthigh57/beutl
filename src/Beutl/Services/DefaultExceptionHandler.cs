﻿using Beutl.Api.Clients;
using Beutl.Logging;
using Microsoft.Extensions.Logging;
using Refit;

namespace Beutl.Services;

public static class DefaultExceptionHandler
{
    private static readonly ILogger s_logger = Log.CreateLogger(typeof(DefaultExceptionHandler));

    public static async ValueTask Handle(this Exception exception)
    {
        if (exception is ApiException apiError)
        {
            try
            {
                var err = await apiError.GetContentAsAsync<ApiErrorResponse>();
                NotificationService.ShowError("API Error", err?.Message ?? apiError.Message);
            }
            catch (Exception ex)
            {
                s_logger.LogError(ex, "Error while handling API error: {ApiContent}", apiError.Content);
                NotificationService.ShowError("API Error", apiError.Message);
            }
        }
        else if (exception is not OperationCanceledException)
        {
            NotificationService.ShowError("Error", exception.Message);
        }
    }
}
