# PublicApi

## What is it?
Given a Visual Studio solution containing .NET Core (C#, VB.NET, F#, ...). displays the public API (public classes with their public or protected members). This is helpful to understand the changes to the public API between two versions of a library 

## How to use?

1. Build the solution PublicApi.sln.

   ```Shell
   mkdir c:\gh
   cd C:\gh
   git clone https://github.com/jmprieur/PublicApi
   dotnet build
   ```
2. Add the output folder to the execution path on your machine.

   ```Shell
   Set PATH=%PATH%;C:\gh\PublicApi\src\PublicApi\net472\bin\debug
   ```
   
3. Go in the folder containing the solution you're interested in analyzing

4. Run the tool

   ```Shell
   PublicApi MySolution.sln > publicApi.txt
   ```

## Understand public API changes between two versions of a library containing several packages.

To understand the differences in the public API between two versions of library, such as Microsoft.Identity.Web, between the current master branch, and a another branch (jmprieur/updateAbstractions1-0-5):
- checkout the first branch or tag that you want to analyze
- run the tool on the solution and redirect the output to a file
- checkout the second branch or tag that you want to analyze
- run the tool on the solution and redirect the output to another file
- diff both files with your favorite diff tool.

```Shell
cd c:\gh
git clone https://github.com/AzureAd/microsoft-identity-web
cd microsoft-identity-web
git checkout master
PublicApi Microsoft.Identity.Web.sln > \temp\Public-Api-Id.Web.1.24.10.txt
git checkout jmprieur/updateAbstractions1-0-5
PublicApi Microsoft.Identity.Web.sln > \temp\Public-Api-Id.Web.2.x.txt
Devenv /diff \temp\Public-Api-Id.Web.1.24.10.txt \temp\Public-Api-Id.Web.2.x.txt 
```

![image](https://user-images.githubusercontent.com/13203188/206930964-9a91361b-f2b0-4644-9bdf-be922c21bd39.png)
