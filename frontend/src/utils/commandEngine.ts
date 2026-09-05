import hrCommandsRaw from '../data/hrCommands.json';
import { INSTRUCTION_COMMANDS_DATA } from '../data/instructionCommands';
import type { InstructionCommandItem } from '../data/instructionCommands';

export interface HRCommand {
  id: string;
  intent: string;
  category: string;
  label: string;
  keywords: string[];
  aliases: string[];
  examples: string[];
  template: string;
  isInstructionGuide?: boolean;
  sectionTitle?: string;
  badge?: string;
  description?: string;
}

export interface CommandSuggestion {
  command: HRCommand;
  displayText: string;
  completedText: string;
  score: number;
  matchedBy: 'prefix' | 'keyword' | 'alias' | 'example' | 'template' | 'fuzzy';
  isInstructionGuide?: boolean;
}

const DEFAULT_DEPARTMENTS = ['IT', 'HR', 'Marketing', 'Operations', 'R&D'];

// Convert Instruction Commands to standard HRCommand format
export const INSTRUCTION_HR_COMMANDS: HRCommand[] = INSTRUCTION_COMMANDS_DATA.map((item: InstructionCommandItem) => ({
  id: item.id,
  intent: item.intent,
  category: item.category,
  label: item.label,
  keywords: item.keywords,
  aliases: item.aliases,
  examples: [item.text],
  template: item.text,
  isInstructionGuide: true,
  sectionTitle: item.sectionTitle,
  badge: item.badge,
  description: item.desc
}));

export const HR_COMMANDS: HRCommand[] = [
  ...INSTRUCTION_HR_COMMANDS,
  ...(hrCommandsRaw as HRCommand[])
];

export function getCommandSuggestions(
  input: string,
  departments: string[] = DEFAULT_DEPARTMENTS
): CommandSuggestion[] {
  const query = input.trim().toLowerCase();
  if (!query) return [];

  const activeDepts = departments.length > 0 ? departments : DEFAULT_DEPARTMENTS;
  const suggestions: CommandSuggestion[] = [];

  // 1. Process Instruction Guide Commands First (Highest Priority)
  for (const instCmd of INSTRUCTION_HR_COMMANDS) {
    let score = 0;
    let matchedBy: CommandSuggestion['matchedBy'] = 'fuzzy';
    const textLower = instCmd.template.toLowerCase();
    const labelLower = instCmd.label.toLowerCase();

    // Exact or prefix match on full instruction text (Direct Ghost Copilot & Dropdown match)
    if (textLower.startsWith(query)) {
      score = 150;
      matchedBy = 'prefix';
    }
    // Prefix match on label
    else if (labelLower.startsWith(query)) {
      score = 140;
      matchedBy = 'prefix';
    }
    // Prefix match on any alias
    else if (instCmd.aliases.some(al => al.toLowerCase().startsWith(query))) {
      score = 130;
      matchedBy = 'alias';
    }
    // Keyword match
    else if (instCmd.keywords.some(kw => kw.toLowerCase().startsWith(query) || query.startsWith(kw.toLowerCase()))) {
      score = 115;
      matchedBy = 'keyword';
    }
    // Substring match inside instruction command text or label
    else if (
      textLower.includes(query) ||
      labelLower.includes(query) ||
      instCmd.keywords.some(kw => kw.toLowerCase().includes(query)) ||
      (instCmd.description && instCmd.description.toLowerCase().includes(query))
    ) {
      score = 95;
      matchedBy = 'example';
    }
    // Fuzzy match
    else if (isFuzzyMatch(query, textLower) || instCmd.keywords.some(kw => isFuzzyMatch(query, kw))) {
      score = 65;
      matchedBy = 'fuzzy';
    }

    if (score > 0) {
      suggestions.push({
        command: instCmd,
        displayText: instCmd.label,
        completedText: instCmd.template,
        score,
        matchedBy,
        isInstructionGuide: true
      });
    }
  }

  // 2. Process General HR Commands
  for (const cmd of (hrCommandsRaw as HRCommand[])) {
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
    else if (cmd.aliases && cmd.aliases.some(al => al.toLowerCase().startsWith(query))) {
      score = 90;
      matchedBy = 'alias';
    }
    // 4. Keyword match
    else if (cmd.keywords && cmd.keywords.some(kw => kw.toLowerCase().startsWith(query) || query.startsWith(kw.toLowerCase()))) {
      score = 80;
      matchedBy = 'keyword';
    }
    // 5. Substring match anywhere in label, category, keywords, or examples
    else if (
      labelLower.includes(query) ||
      (cmd.category && cmd.category.toLowerCase().includes(query)) ||
      (cmd.keywords && cmd.keywords.some(kw => kw.toLowerCase().includes(query))) ||
      (cmd.examples && cmd.examples.some(ex => ex.toLowerCase().includes(query))) ||
      intentLower.includes(query)
    ) {
      score = 60;
      matchedBy = 'template';
    }
    // 6. Basic fuzzy match (tolerates minor typos like allocat, budgt, onboar)
    else if (isFuzzyMatch(query, labelLower) || (cmd.keywords && cmd.keywords.some(kw => isFuzzyMatch(query, kw)))) {
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
        matchedBy,
        isInstructionGuide: false
      });
    }
  }

  // Deduplicate by completedText
  const uniqueSuggestions: CommandSuggestion[] = [];
  const seenTexts = new Set<string>();

  for (const sug of suggestions) {
    const key = sug.completedText.trim().toLowerCase();
    if (!seenTexts.has(key)) {
      seenTexts.add(key);
      uniqueSuggestions.push(sug);
    }
  }

  // Sort by score descending
  uniqueSuggestions.sort((a, b) => b.score - a.score);

  // Return top 8 unique suggestions
  return uniqueSuggestions.slice(0, 8);
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
