const fs = require('fs');
const path = require('path');

const broken = [];
function walk(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) walk(file);
    else if (entry.name.endsWith('.md')) {
      const source = fs.readFileSync(file, 'utf8');
      for (const match of source.matchAll(/\]\(([^)#]+)(?:#[^)]+)?\)/g)) {
        const link = match[1];
        if (link.startsWith('http') || link.startsWith('mailto:')) continue;
        const target = path.resolve(path.dirname(file), link.replace(/\\/g, '/'));
        if (!fs.existsSync(target)) broken.push(`${file} -> ${link}`);
      }
    }
  }
}

walk('docs');
if (broken.length) {
  console.log('BROKEN LINKS');
  console.log(broken.join('\n'));
  process.exitCode = 1;
} else {
  console.log('All relative Markdown links resolve.');
}
