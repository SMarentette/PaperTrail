<h1><img src="Assets/mainIcon_100.png" alt="PaperTrail Icon" width="36" /> PaperTrail</h1>

PaperTrail is a standalone markdown viewer/editor built with Avalonia.

## What It Does
- Browse markdown files in a folder explorer.
- Toggle **View Mode** and **Edit Mode** with synced preview.
- Switch between **Dark** and **Light** markdown themes.
- Use an **On This Page** sidebar for fast heading navigation.

## Config + Template
- App config is stored in: `%AppData%\PaperTrail\settings.json`
- New file template is stored in: `%AppData%\PaperTrail\markdown.md`
- When you open a new folder in PaperTrail, that folder is saved to `settings.json`.
- Clicking **New Markdown** creates a file from `markdown.md`.

## Styling
- Dark mode uses `Styles/atom-dark.css`.
- Light mode uses `Styles/newsprint.css`.
- The app shell is theme-aware (ribbon, explorer, sidebar, editor workspace).

## Demo Video
<video src="Assets/PaperTrail.mp4" controls width="960"></video>

## How to Use
```bash
git clone <your-repo-url>
cd PaperTrail
dotnet restore
dotnet run --project PaperTrail.csproj
```

## Contributing
I am open to contributions.

1. Fork the repo.
2. Clone your fork.
3. Create a branch for your change.
4. Open a pull request.

## License
This project is licensed under the MIT License. See `LICENSE`.
