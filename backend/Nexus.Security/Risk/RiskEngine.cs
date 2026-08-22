using Nexus.Data.Enums;

namespace Nexus.Security.Risk;

public interface IRiskEngine
{
    RiskLevel EvaluateToolRisk(string toolName, string argumentsJson);
    bool RequiresApproval(RiskLevel riskLevel, string userRole = "Admin");
}

public class RiskEngine : IRiskEngine
{
    public RiskLevel EvaluateToolRisk(string toolName, string argumentsJson)
    {
        var nameLower = (toolName ?? string.Empty).ToLower();
        var argsLower = (argumentsJson ?? string.Empty).ToLower();

        if (nameLower.Contains("delete") || argsLower.Contains("delete all"))
        {
            return RiskLevel.Critical;
        }

        if (nameLower.Contains("update") || argsLower.Contains("increase") || argsLower.Contains("salary"))
        {
            return RiskLevel.High;
        }

        if (nameLower.Contains("create") || nameLower.Contains("onboard"))
        {
            return RiskLevel.Medium;
        }

        return RiskLevel.Low;
    }

    public bool RequiresApproval(RiskLevel riskLevel, string userRole = "Admin")
    {
        return riskLevel switch
        {
            RiskLevel.Low => false,
            RiskLevel.Medium => false,
            RiskLevel.High => true,
            RiskLevel.Critical => true,
            _ => false
        };
    }
}
