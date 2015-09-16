namespace TimestampedJsonLogFilter

open TimestampedJsonLogFilter.Types
open System.IO
open System
open Newtonsoft.Json
open Newtonsoft.Json.Linq

module Parse =

  module Internal =
    type Externals = {
      DirFileFinder : string -> string list
      LineReader : string -> string list
      FileWriter : string -> string list -> bool
      DateTimeParser : string -> DateTime
      JObjectParser : string -> JObject
    }

    let mutable externals = {
      DirFileFinder =
        (fun (dir:string) ->
          (new DirectoryInfo(dir)).GetFiles()
          |> Array.map (fun fi -> fi.Name)
          |> Array.toList
        )
      LineReader =
        (fun fn ->
          File.ReadAllLines fn
          |> Array.toList
        )
      FileWriter =
        (fun fn lines ->
          File.WriteAllLines(fn,(List.toArray lines))
          true
        )
      DateTimeParser = DateTime.Parse
      JObjectParser = JObject.Parse
    }

    let filesInDir dir =
      externals.DirFileFinder dir
      |> List.filter (fun fn -> fn.EndsWith(".log"))

    let lineToTimeData (line:string) =
      try
        match line.Split('\t') with
          | [| time ; payload |] ->
            externals.DateTimeParser(time) , externals.JObjectParser(payload)
          | _ -> raise (new Exception(sprintf "Invalid line %s" line))
      with
        | _ -> raise (new Exception(sprintf "Invalid line %s" line))

    let fileToLines root filename =
      let fullname = sprintf "%s/%s" root filename
      printfn "parse %s" fullname
      let lines =
        fullname
        |> externals.LineReader
        |> List.map lineToTimeData
      lines, filename

    let filesToLines root files =
      List.map (fileToLines root) files

    let linesToFileObject earliest (lines, filename) =
       {
          Filename = filename
          Lines =
            lines
            |> List.map (fun (time, data) ->
              {
                Time = time - earliest
                Data = data
              }
            )
       }

    let linesToFileObjects filesLines =
      let (earliestLines, _) =
        filesLines
        |> List.minBy (fun (lines, _) ->
          fst (List.minBy fst lines)
        )
      let earliest = fst (List.head earliestLines)
      let files =List.map (linesToFileObject earliest) filesLines
      earliest, files

    let transformLine (time:DateTime) (line:LogLine) =
      let time = (time + line.Time).ToString("yyyy-MM-ddTHH:mm:ss.ffff")
      let data = line.Data.ToString(Formatting.None)
      sprintf "%s\t%s" time data

    let writeFile directory time (logFile:LogFile) =
      let fn = sprintf "%s/%s" directory logFile.Filename
      let lines =
        logFile.Lines
        |> List.map (transformLine time)
      externals.FileWriter fn lines

  let fromDirectory (directory:string) : Log =
    let (earliest, files) =
      directory
      |> Internal.filesInDir
      |> Internal.filesToLines directory
      |> Internal.linesToFileObjects
    {
      Files = files
      Time = earliest
    }

  let toDirectory (directory:string) (log:Log) =
    log.Files
    |> List.map (Internal.writeFile directory log.Time)
    |> ignore
