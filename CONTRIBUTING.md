# Contributing to SaanSoft.TaggedCache

## Recommended Git Setup

### Git Config

This repo uses the line endings `LF`.

Run the following to configure git config, especially if you are working on Windows.

```shell
git config core.autocrlf input
git config core.eol lf
```

### Git Hooks

All PRs will be verified against linting scripts to ensure styling consistency.
For a faster feedback cycle, we recommend configuring git hooks.

Run the following to enable git hooks for this repository.

```shell
git config core.hooksPath .githooks
```

### Signed commits

So we can attribute changes to the people involved we like to have signed commits

```shell
git config user.name "John Doe"
git config user.email jdoe@example.com

# Or to configure globally for all repositories
git config --global user.name "John Doe"
git config --global user.email jdoe@example.com
```

Then use the `-s` or `--signoff` flag when committing.

## Spelling

SaanSoft.TaggedCache uses the `en-GB` dictionary for spell checking.

The `dictionary.dic` file at the root level of the repository contains project specific words.

### VS Code

Add the [Code Spell Checker](https://marketplace.visualstudio.com/items?itemName=streetsidesoftware.code-spell-checker) plugin and enable it.

The `.cspell.json` file in the root directory is configured to use and add any unknown words to the dictionary.

The `en-GB` dictionary comes packaged with the plugin, so no further setup is required.

### JetBrains Rider

#### Configure project to use `dictionary.dic

Open the project then go to:

* `Settings` > `Editor` > `Spelling`
* Check `Use single dictionary for saving words` and select `application-level`
* Under `Custom dictionaries (plain text word lists, hunspell)`
  * Click the `+` button
  * In the root directory of the repository, select `dictionary.dic`
  * Save the changes

### Visual Studio

Read more [here](https://learn.microsoft.com/en-us/visualstudio/ide/text-spell-checker)

## Coding guidelines

A consistent coding style is included via [EditorConfig](https://editorconfig.org/) with the file [.editorconfig](./.editorconfig) at the root of the repo. Depending on your editor of choice, it will either support it out of the box or you can [download a plugin](https://editorconfig.org/#download) for the config to be applied.
