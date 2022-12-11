# PublicApi

Usage:
Add PublicApi.exe to the execution path on your machine.

```Shell
PublicApi MySolution.sln > publicApi.txt
```

Example to understand the differences in the public API between two versions of Microsoft.Identity.Web (the current version in the master branch, and a future version in another branch.

```Shell
git clone https://github.com/AzureAd/microsoft-identity-web
cd microsoft-identity-web
git checkout master
PublicApi Microsoft.Identity.Web.sln > \temp\Public-Api-Id.Web.1.24.10.txt
git checkout jmprieur/updateAbstractions1-0-5
PublicApi Microsoft.Identity.Web.sln > \temp\Public-Api-Id.Web.2.x.txt
Devenv /diff \temp\Public-Api-Id.Web.1.24.10.txt \temp\Public-Api-Id.Web.2.x.txt 
```

![image](https://user-images.githubusercontent.com/13203188/206930964-9a91361b-f2b0-4644-9bdf-be922c21bd39.png)
