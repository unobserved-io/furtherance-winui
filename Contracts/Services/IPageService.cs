using System;

namespace Furtherance.Contracts.Services;

public interface IPageService
{
    Type GetPageType(string key);
}
