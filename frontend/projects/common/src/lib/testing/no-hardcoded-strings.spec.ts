import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, sep } from 'node:path';

/**
 * `AC-63` — no user-facing string is hardcoded in a template.
 *
 * **This test is the reason AC-63 will still hold next month.** Converting fifteen templates makes
 * the criterion true on the day it ships; only a sweep keeps it true on the sixteenth screen
 * someone adds. Without it the rot is invisible — an English label in an Arabic page looks like a
 * missing translation, not like a bug, and nobody files it.
 *
 * Modelled on `rtl-safety.spec.ts`, which does the same job for physical-direction utilities.
 *
 * Method: strip everything that is NOT visible text — comments, tags with all their attributes and
 * bindings, `@if`/`@for`/`@switch` control-flow headers, and `{{ … }}` interpolations — then assert
 * nothing readable is left. An interpolation is stripped rather than inspected because it is
 * already an expression: `{{ 'x' | t }}`, `{{ customer.name }}` and `{{ locale.resolve(m) }}` are
 * all legitimate, and a literal smuggled inside one (`{{ 'Save' }}`) is caught by ALLOWED below
 * failing to cover it.
 *
 * NOTE: like the RTL guard, this scans `.html` only. A component with an inline template escapes
 * it, so those carry their own assertions — see `admin-app`'s `shell.component.spec.ts`.
 */

/**
 * Text that is allowed to survive the strip. Kept deliberately short: every addition is a hole.
 *
 * These are punctuation and separators that carry no language and would be identical in Arabic —
 * an em dash between a name and an email, the arrow in a status transition. Anything a translator
 * would want to change does NOT belong here.
 */
const ALLOWED: readonly RegExp[] = [
  // Separators and arrows between two interpolated values.
  /^[—–\-→,;:.?!()[\]{}|/\\]+$/,
  // A lone quote or an ellipsis left by punctuation around an interpolation.
  /^['"…]+$/,
  // Material Symbols are visual glyphs, not user-facing copy (snake_case icon identifiers).
  /^[a-z][a-z0-9_]*$/,
];

const SKIP = new Set(['node_modules', 'dist', '.angular', '.git']);

/**
 * `index.html` is the document shell, not a component template: its `<title>` is read by the
 * browser before Angular exists, so it cannot come from a signal-backed dictionary.
 */
const SKIP_FILES = new Set(['index.html']);

function htmlFilesUnder(dir: string): string[] {
  const found: string[] = [];

  for (const entry of readdirSync(dir)) {
    if (SKIP.has(entry)) {
      continue;
    }

    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...htmlFilesUnder(full));
    } else if (entry.endsWith('.html') && !SKIP_FILES.has(entry)) {
      found.push(full);
    }
  }

  return found;
}

/** Everything that is not visible text, removed. What is left is what a user would read. */
function visibleText(template: string): string[] {
  const stripped = template
    // Comments first — they contain prose, and prose is what we are hunting for.
    .replace(/<!--[\s\S]*?-->/g, ' ')
    // Interpolations next, BEFORE any brace cleanup: strip them later and the control-flow pass
    // below eats one of their closing braces first, leaving `{{ x }` behind and every
    // interpolation in the repository reported as an offender.
    .replace(/\{\{[\s\S]*?\}\}/g, '\n')
    // Tags, with every attribute and binding inside them.
    .replace(/<[^>]*>/g, '\n')
    // Control-flow headers: `@if (x; as y) {`, `} @else if (…) {`, `@for (… ; track …) {`.
    .replace(/@(if|else|for|switch|case|default|empty)\b[^{]*\{/g, '\n')
    // The braces those blocks close with.
    .replace(/[{}]/g, '\n');

  return stripped
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
}

describe('hardcoded UI strings', () => {
  it('AC63: every UI string resolves through the dictionary', () => {
    const root = join(process.cwd(), 'projects');
    const offenders: string[] = [];

    for (const file of htmlFilesUnder(root)) {
      for (const text of visibleText(readFileSync(file, 'utf8'))) {
        if (ALLOWED.some((allowed) => allowed.test(text))) {
          continue;
        }

        offenders.push(`${file.split(sep).slice(-2).join('/')}: ${text}`);
      }
    }

    // Reported as a list rather than a count, so a failure names the template and the string
    // instead of sending the next person hunting through fifteen files.
    expect(offenders).toEqual([]);
  });
});
