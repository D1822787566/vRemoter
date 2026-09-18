using WindowsRawInputProbe;

var config = MappingConfig.Parse("{\"keys\":{\"VK_BROWSER_HOME\":{\"single\":\"copy\",\"double\":\"paste\"},\"LEFT\":{\"single\":\"right\"}}}");
if (config.Get("VK_BROWSER_HOME").Double != ActionKind.Paste) throw new Exception("browser-home double mapping failed");
if (config.Get("LEFT").Single != ActionKind.Right) throw new Exception("left mapping failed");
Console.WriteLine("PASS");
