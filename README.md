#  Easy, Affable, and Secure Youtube (EASY) API
A Youtube API written in C# with ![Carter](https://github.com/CarterCommunity/Carter)

## Features
- On the fly conversion from Youtube AAC stream to MP3 using FFMPEG
- One hour cache for preconverted audio
- Basic API Key Authentication
- ![Info API](#info)

## Installation
1. Download either a prebuilt binary ![here](https://github.com/CanadianBaconBoi/YoutubeAPI/releases) or build it yourself with the below instructions
2. Open the containing folder and modify `config.json` with your preferred values
    - Youtube API key needs to be created using ![these instructions](https://developers.google.com/youtube/v3/getting-started).
3. Run the binary (either YoutubeAPI.exe or `dotnet YoutubeAPI.dll` in the CLI)

## Usage
There are two endpoints which both expect certain query parameters.
### `/audio`
- `key` (optional¹) : A key inside of the APIKeys array in `config.json`
- `id` (required) : An ID of a Youtube video 
- Example: `http://127.0.0.1/audio/?key=133742069&id=dQw4w9WgXcQ`

### `/info`
- `key` (optional¹) : A key inside of the APIKeys array in `config.json`
- `id` (required) : An ID of a Youtube video
- `format` (optional) [default: json] : A format, either `json` or `e2`.
    - `e2` is a custom raw data format for a personal application.
- Example: `http://127.0.0.1/info/?key=133742069&id=dQw4w9WgXcQ&format=json`



¹ `key` is only required if APIKeys have been set in `config.json`


## Build
### Requirements:
One of
- ![Visual Studio 2022 Community](https://visualstudio.microsoft.com/thank-you-downloading-visual-studio/?sku=Community&channel=Release&version=VS2022)
    -  With Desktop Development using .NET installed
- ![.NET SDK 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-6.0.202-windows-x64-installer) (for CLI)

### Instructions:
1. Clone this repo `git clone https://github.com/CanadianBaconBoi/YoutubeAPI.git`
2. Change directories to the folder
#### Using Visual Studio
  >3. Open YoutubeAPI.sln in Visual Studio 2022
  >4. Navigate to the top menu "Build", and click "Build Solution"
#### Using CLI
  >3. Open a commandline in the directory
  >4. Run `dotnet publish YoutubeAPI.sln --configuration Release`
5. Locate your built binary in bin/Release/net6.0/



### To-do
- [ ] Implement better authentication
- [ ] Improve cache implementation
- [ ] Improve logging
