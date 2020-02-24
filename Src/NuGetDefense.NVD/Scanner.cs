using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MessagePack;
using NuGet.Versioning;
using NuGetDefense.Core;

namespace NuGetDefense.NVD
{
    public class Scanner
    {
        private readonly Dictionary<string, Dictionary<string, VulnerabilityEntry>> nvdDict;

        public Scanner(string nugetFile, bool breakIfCannotRun = false)
        {
            var lz4Options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithSecurity(MessagePackSecurity.UntrustedData);
            var vulnDataFile = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                "VulnerabilityData.bin");

            var nvdData = File.Open(vulnDataFile, FileMode.Open, FileAccess.Read);
            nvdDict = MessagePackSerializer
                .Deserialize<
                    Dictionary<string, Dictionary<string, VulnerabilityEntry>>>(nvdData, lz4Options);

            NugetFile = nugetFile;
            BreakIfCannotRun = breakIfCannotRun;
        }

        private string NugetFile { get; }
        private bool BreakIfCannotRun { get; }

        public Dictionary<string, Dictionary<string, Vulnerability>> GetVulnerabilitiesForPackages(NuGetPackage[] pkgs,
            Dictionary<string, Dictionary<string, Vulnerability>> vulnDict = null)
        {
            try
            {
                if (vulnDict == null) vulnDict = new Dictionary<string, Dictionary<string, Vulnerability>>();
                foreach (var pkg in pkgs)
                {
                    var pkgId = pkg.Id.ToLower();
                    if (!nvdDict.ContainsKey(pkgId)) continue;
                    if (!vulnDict.ContainsKey(pkgId)) vulnDict.Add(pkgId, new Dictionary<string, Vulnerability>());
                    foreach (var cve in nvdDict[pkgId].Keys.Where(cve => nvdDict[pkgId][cve].Versions.Any(v =>
                        VersionRange.Parse(v).Satisfies(new NuGetVersion(pkg.Version)))))
                        vulnDict[pkgId].Add(cve, ToVulnerability(cve, nvdDict[pkgId][cve]));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    $"{NugetFile} : {(BreakIfCannotRun ? "Error" : "Warning")} : NuGetDefense : NVD scan failed with exception: {e}");
            }

            return vulnDict;
        }

        public Vulnerability ToVulnerability(string cve,
            VulnerabilityEntry vulnerability)
        {
            return new Vulnerability
            {
                Cve = cve,
                Description = vulnerability.Description,
                Cwe = vulnerability.Cwe,
                Vendor = vulnerability.Vendor,
                CvssScore = vulnerability.Score ?? -1,
                Vector = vulnerability.Vector
            };
        }
    }
}