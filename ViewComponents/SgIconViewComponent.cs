using Microsoft.AspNetCore.Mvc;

namespace SecureGate1.ViewComponents;

public class SgIconViewComponent : ViewComponent
{
    public IViewComponentResult Invoke(string name) => View("Default", name);
}
