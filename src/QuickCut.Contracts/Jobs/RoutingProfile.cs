using System.Runtime.Serialization;

namespace QuickCut.Contracts.Jobs;

public enum RoutingProfile
{
    [EnumMember(Value = "offline-only")]
    OfflineOnly,

    [EnumMember(Value = "local-web")]
    LocalWeb,

    [EnumMember(Value = "code-agent")]
    CodeAgent,

    [EnumMember(Value = "cloud-fallback")]
    CloudFallback,
}
