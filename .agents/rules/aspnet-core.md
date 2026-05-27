# ASP.NET Core Rule

Apply this rule when editing `.cs`, `.cshtml`, `.csproj`, or `appsettings*.json` files.

- Use ASP.NET Core built-in DI, configuration, logging, Identity, authorization, validation, and antiforgery.
- Keep actions/page handlers async when they call I/O.
- Keep MVC controllers thin and delegate business logic to ServiceLayer.
- Use EF Core LINQ or parameterized SQL, never string-concatenated SQL.
- Run migrations using DataAccessLayer as the target project and MVC as the startup project.
- Keep test projects aligned with the app they verify.

