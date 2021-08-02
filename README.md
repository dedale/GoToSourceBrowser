# GoToSourceBrowser

Simple Visual Studio Extension (vsix) to open current code in [Source Browser](https://github.com/KirillOsenkov/SourceBrowser).

This is useful when current solution do not include all repo projects.

# Load

Extension is automatically loaded when opening a solution.

# Options

Each repository can be associated with a dedicated Source Browser URI.

To match repositories and URIs, it uses the name of the repository in git remote URIs.

![Options](doc/Options.png)

# Logs

Logs are generated in `%TEMP%\GoToSourceBrowser` folder.

