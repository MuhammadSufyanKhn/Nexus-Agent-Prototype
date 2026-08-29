import hrCommandsRaw from '../data/hrCommands.json';

export interface HRCommand {
  id: string;
  intent: string;
  category: string;
  label: string;
  keywords: string[];
  aliases: string[];
  examples: string[];
  template: string;
}

export interface CommandSuggestion {
  command: HRCommand;
  displayText: string;
  completedText: string;
  score: number;
  matchedBy: 'prefix' | 'keyword' | 'alias' | 'example' | 'template' | 'fuzzy';
}

const DEFAULT_DEPARTMENTS = ['IT', 'HR', 'Marketing', 'Operations', 'R&D'];

export const HR_COMMANDS: HRCommand[] = hrCommandsRaw as HRCommand[];

export function getCommandSuggestions(
  input: string,
  departments: string[] = DEFAULT_DEPARTMENTS
): CommandSuggestion[] {
  const query = input.trim().toLowerCase();
  if (!query) return [];

  const activeDepts = departments.length > 0 ? departments : DEFAULT_DEPARTMENTS;
  const suggestions: CommandSuggestion[] = [];

  for (const cmd of HR_COMMANDS) {
    let score = 0;
    let matchedBy: CommandSuggestion['matchedBy'] = 'fuzzy';
    let bestCompletedText = cmd.examples[0] || cmd.template;

    const labelLower = cmd.label.toLowerCase();
    const intentLower = cmd.intent.toLowerCase();

    // 1. Exact or prefix match on label
    if (labelLower.startsWith(query)) {
      score = 100;
      matchedBy = 'prefix';
    }
    // 2. Exact or prefix match on any example
    else if (cmd.examples.some(ex => ex.toLowerCase().startsWith(query))) {
      score = 95;
      matchedBy = 'example';
      const matchingEx = cmd.examples.find(ex => ex.toLowerCase().startsWith(query));
      if (matchingEx) bestCompletedText = matchingEx;
    }
    // 3. Prefix match on aliases
    else if (cmd.aliases.some(al => al.toLowerCase().startsWith(query))) {
      score = 90;
      matchedBy = 'alias';
    }
    // 4. Keyword match
    else if (cmd.keywords.some(kw => kw.toLowerCase().startsWith(query) || query.startsWith(kw.toLowerCase()))) {
      score = 80;
      matchedBy = 'keyword';
    }
    // 5. Substring match anywhere in label, category, keywords, or examples
    else if (
      labelLower.includes(query) ||
      cmd.category.toLowerCase().includes(query) ||
      cmd.keywords.some(kw => kw.toLowerCase().includes(query)) ||
      cmd.examples.some(ex => ex.toLowerCase().includes(query)) ||
      intentLower.includes(query)
    ) {
      score = 60;
      matchedBy = 'template';
    }
    // 6. Basic fuzzy match (tolerates minor typos like allocat, budgt, onboar)
    else if (isFuzzyMatch(query, labelLower) || cmd.keywords.some(kw => isFuzzyMatch(query, kw))) {
      score = 40;
      matchedBy = 'fuzzy';
    }

    if (score > 0) {
      // Intelligently resolve template placeholders with real department context
      let resolvedText = bestCompletedText;

      // If user typed "allocate 100k", adapt completedText to keep user's explicit values
      if (cmd.id === 'budget_update') {
        const deptMatch = activeDepts.find(d => query.includes(d.toLowerCase()));
        const targetDept = deptMatch || activeDepts[0] || 'IT';
        if (query.includes('allocate') || query.includes('add') || query.includes('set')) {
          resolvedText = `Allocate 100k budget to ${targetDept} department`;
        }
      } else if (cmd.id === 'budget_reallocate') {
        const srcDept = activeDepts[2] || 'Marketing';
        const tgtDept = activeDepts[1] || 'HR';
        resolvedText = `Reallocate 100000 budget from ${srcDept} to ${tgtDept} department`;
      } else if (cmd.id === 'budget_freeze') {
        const targetDept = activeDepts.find(d => query.includes(d.toLowerCase())) || activeDepts[2] || 'Marketing';
        resolvedText = `Freeze the ${targetDept} department budget due to Q3 audit review`;
      } else {
        // Substitute template placeholders gracefully
        resolvedText = resolvedText
          .replace('{department}', activeDepts[0] || 'IT')
          .replace('{sourceDepartment}', activeDepts[2] || 'Marketing')
          .replace('{targetDepartment}', activeDepts[1] || 'HR')
          .replace('{amount}', '$100,000')
          .replace('{employeeName}', 'Umar Danish')
          .replace('{designation}', 'Senior Product Manager')
          .replace('{policyName}', 'remote work')
          .replace('{salary}', '$120,000')
          .replace('{percentage}', '10')
          .replace('{quarter}', 'Q3')
          .replace('{days}', '5')
          .replace('{weeks}', '12')
          .replace('{year}', '2026')
          .replace('{headName}', 'Sufyan Khan')
          .replace('{oldDepartment}', 'IT')
          .replace('{newDepartment}', 'Digital Technology')
          .replace('{softwareName}', 'GitHub Copilot')
          .replace('{expenseType}', 'international flight bookings')
          .replace('{cohortName}', 'Summer 2026 Interns')
          .replace('{mentorName}', 'Tariq Mahmood')
          .replace('{managerName}', 'Tariq Mahmood')
          .replace('{date}', 'March 31')
          .replace('{count}', '50');
      }

      suggestions.push({
        command: cmd,
        displayText: cmd.label,
        completedText: resolvedText,
        score,
        matchedBy
      });
    }
  }

  // Sort by score descending
  suggestions.sort((a, b) => b.score - a.score);

  // Return top 8 unique suggestions
  return suggestions.slice(0, 8);
}

function isFuzzyMatch(query: string, text: string): boolean {
  if (query.length < 3) return false;
  let qIdx = 0;
  for (let i = 0; i < text.length && qIdx < query.length; i++) {
    if (text[i] === query[qIdx]) {
      qIdx++;
    }
  }
  return qIdx >= Math.min(query.length, 4);
}
