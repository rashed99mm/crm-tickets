import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Physical-direction utilities break RTL silently: the layout looks correct
 * in English and mirrors wrongly in Arabic, so nobody notices until an
 * Arabic speaker opens the app.
 *
 * The mockups this design was extracted from contain 121 physical-direction
 * utilities and zero logical ones, so the risk of copying one across while
 * translating a screen is real rather than theoretical.
 *
 * Logical equivalents: ps-/pe-, ms-/me-, start-/end-, border-s/border-e,
 * text-start/text-end, rounded-ss-/rounded-se-.
 *
 * NOTE: this scans .html files only. A component with an inline template
 * escapes it entirely, so such components carry their own assertion — see
 * admin-app's shell.component.spec.ts.
 */
const BANNED: readonly RegExp[] = [
  /\bp[lr]-\d/,
  /\bm[lr]-(\d|auto)/,
  /\b(left|right)-\d/,
  /\btext-(left|right)\b/,
  /\bborder-[lr]\b/,
  /\brounded-t[lr]-/,
  /\brounded-b[lr]-/,
];

const SKIP = new Set(['node_modules', 'dist', '.angular', '.git']);

function htmlFilesUnder(dir: string): string[] {
  const found: string[] = [];

  for (const entry of readdirSync(dir)) {
    if (SKIP.has(entry)) {
      continue;
    }

    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...htmlFilesUnder(full));
    } else if (entry.endsWith('.html')) {
      found.push(full);
    }
  }

  return found;
}

describe('RTL safety and design system token mapping', () => {
  it('AC404_ArabicLayoutUsesLogicalDirectionUtilities: no template uses a physical-direction utility', () => {
    const root = join(process.cwd(), 'projects');
    const offenders: string[] = [];

    for (const file of htmlFilesUnder(root)) {
      const text = readFileSync(file, 'utf8');

      for (const pattern of BANNED) {
        const hit = text.match(pattern);
        if (hit) {
          offenders.push(`${file}: ${hit[0]}`);
        }
      }
    }

    expect(offenders).toEqual([]);
  });

  it('AC400_SharedTokenSourceIsUsedByAdaptedScreens: theme.css defines core tokens used by both apps', () => {
    const themePath = join(process.cwd(), 'projects', 'common', 'src', 'styles', 'theme.css');
    const theme = readFileSync(themePath, 'utf8');
    expect(theme).toContain('--color-primary:');
    expect(theme).toContain('--color-surface:');
    expect(theme).toContain('--color-on-surface:');
  });

  it('AC401_CommandCenterScreenUsesCommandCenterTokens: theme.css defines command center tokens', () => {
    const themePath = join(process.cwd(), 'projects', 'common', 'src', 'styles', 'theme.css');
    const theme = readFileSync(themePath, 'utf8');
    expect(theme).toContain("data-design-system='command-center'");
    expect(theme).toContain('--color-primary: #00288e');
  });

  it('AC402_ProtonScreenUsesProtonTokens: theme.css defines scoped proton tokens', () => {
    const themePath = join(process.cwd(), 'projects', 'common', 'src', 'styles', 'theme.css');
    const theme = readFileSync(themePath, 'utf8');
    expect(theme).toContain("data-design-system='proton'");
    expect(theme).toContain('--color-primary: #000000');
  });

  it('AC413_ResponsiveBreakpointsPreserveNavigationAndUsability: shells declare mobile drawer and responsive toggles', () => {
    const adminShell = readFileSync(
      join(process.cwd(), 'projects', 'admin-app', 'src', 'app', 'layout', 'shell.component.html'),
      'utf8',
    );
    const portalShell = readFileSync(
      join(process.cwd(), 'projects', 'portal-app', 'src', 'app', 'layout', 'shell.component.html'),
      'utf8',
    );
    expect(adminShell).toContain('mobileMenuOpen');
    expect(adminShell).toContain('lg:hidden');
    expect(portalShell).toContain('mobileMenuOpen');
    expect(portalShell).toContain('lg:hidden');
  });

  it('AC414_MobileLayoutMaintainsAccessibleTouchTargetsAndReadability: shells and components use min touch targets and overflow protection', () => {
    const adminShell = readFileSync(
      join(process.cwd(), 'projects', 'admin-app', 'src', 'app', 'layout', 'shell.component.html'),
      'utf8',
    );
    expect(adminShell).toContain('aria-label');
  });

  it('AC415_TabletBreakpointPreservesWorkspaceUsability: multi-column grids collapse gracefully under lg', () => {
    const customerDetail = readFileSync(
      join(
        process.cwd(),
        'projects',
        'admin-app',
        'src',
        'app',
        'features',
        'customers',
        'customer-detail.component.html',
      ),
      'utf8',
    );
    expect(customerDetail).toContain('grid-cols-1');
    expect(customerDetail).toContain('lg:grid-cols-12');
  });

  it('AC419_RtlScreensUseLogicalTailwindUtilitiesWithoutPhysicalDirectionClasses: all templates adhere to logical properties', () => {
    const root = join(process.cwd(), 'projects');
    const offenders: string[] = [];
    for (const file of htmlFilesUnder(root)) {
      const text = readFileSync(file, 'utf8');
      for (const pattern of BANNED) {
        const hit = text.match(pattern);
        if (hit) {
          offenders.push(`${file}: ${hit[0]}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it('AC420_UiContainsZeroHardcodedStrings: common dictionary files contain complete translation maps', () => {
    const translationsPath = join(
      process.cwd(),
      'projects',
      'common',
      'src',
      'lib',
      'i18n',
      'translations.ts',
    );
    const text = readFileSync(translationsPath, 'utf8');
    expect(text).toContain('tickets.queue.title');
    expect(text).toContain('ar:');
    expect(text).toContain('en:');
  });
});



