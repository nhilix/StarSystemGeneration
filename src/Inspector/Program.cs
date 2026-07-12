using StarGen.Inspector;

// headless modes dispatch on the first arg; bare invocation is the REPL
if (args.Length >= 2 && args[0] == "sweep")
    return SweepRunner.Run(args[1]);

new Repl().Run();
return 0;
