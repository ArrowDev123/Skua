/*
name: CustomImportSmoke
description: Verifies custom sibling and supplied CoreBots imports.
tags: test, imports
*/
//cs_include CustomImportSibling.cs
//cs_include CoreBots.cs
using Skua.Core.Interfaces;
using System.Threading;

public class CustomImportSmoke
{
    public void ScriptMain(IScriptInterface Bot)
    {
        Bot.Log(CustomImportSibling.Marker);
        _ = CoreBots.Instance;

        Bot.Log("Custom imports verified. Stop the script when finished.");
        while (!Bot.ShouldExit)
            Thread.Sleep(100);
    }
}
