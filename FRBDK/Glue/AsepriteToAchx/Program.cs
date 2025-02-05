﻿// See https://aka.ms/new-console-template for more information

using AsepriteDotNet;
using AsepriteDotNet.Image;
using AsepriteToAchx;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Graphics.Animation;
using FlatRedBall.IO;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

const string BaseDirectory = "InputFiles";
const string AnimationFileName = "TestAnimations";
// const string AsepriteFileName = "Robot96";
const string AsepriteFileName = "Robot96";
const string JsonFileName = "Robot96Attacks";
const string JsonFrameFormat = "{tag}:{tagframe}";
const bool NormalizeAnimationTimes = true;
string projectPath = ProjectSourcePath.Value;
string inputDirectory = Path.Combine(projectPath, "InputFiles");
string outputDirectory = Path.Combine(projectPath, "OutputFiles");

var asepriteFile = AsepriteFile.Load(Path.Combine(inputDirectory, AsepriteFileName + ".aseprite"));
SpritesheetOptions sheetOptions = new SpritesheetOptions
{
    OnlyVisibleLayers = true,
    MergeDuplicates = true,
    PackingMethod = PackingMethod.SquarePacked,
};
var spritesheet = asepriteFile.ToSpritesheet(sheetOptions);

string jsonString = File.ReadAllText(Path.Combine(inputDirectory, JsonFileName + ".json"));
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
};
var sheetData = JsonSerializer.Deserialize<AsepriteSheetData>(jsonString, options);

// var newChainList = Mapper.Map(sheetData);
var newChainList = Mapper.MapWithCollision(sheetData, asepriteFile, NormalizeAnimationTimes);

FileManager.XmlSerialize(newChainList, out string serializedChainList);
File.WriteAllText(Path.Combine(outputDirectory, JsonFileName + "Generated.achx"), serializedChainList);

Console.WriteLine();
