import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const frontend = dirname(fileURLToPath(import.meta.url));
const repository = resolve(frontend, '../..');
const read = (path) => readFileSync(resolve(repository, path), 'utf8');

test('Angular acceptance build replaces only the public-demo environment', () => {
  const workspace = JSON.parse(read('src/frontend/angular.json'));
  const build = workspace.projects.frontend.architect.build;
  const publicDemo = build.configurations['public-demo'];
  const acceptance = build.configurations['public-demo-acceptance'];
  assert.equal(build.defaultConfiguration, 'production');
  assert.deepEqual(publicDemo.fileReplacements, [{
    replace: 'src/environments/environment.ts',
    with: 'src/environments/environment.public-demo.ts',
  }]);
  assert.deepEqual(acceptance.fileReplacements, [{
    replace: 'src/environments/environment.ts',
    with: 'src/environments/environment.public-demo-acceptance.ts',
  }]);
  assert.deepEqual({ ...acceptance, fileReplacements: publicDemo.fileReplacements }, publicDemo);
  assert.equal(publicDemo.outputHashing, 'all');

  const scripts = JSON.parse(read('src/frontend/package.json')).scripts;
  assert.equal(scripts['build:public-demo'], 'ng build frontend --configuration public-demo');
  assert.equal(scripts['build:public-demo-acceptance'],
    'ng build frontend --configuration public-demo-acceptance');
});

test('Docker frontend build has an exact two-value allowlist and safe default', () => {
  const dockerfile = read('deploy/public-demo/frontend.Dockerfile');
  assert.match(dockerfile, /^ARG FRONTEND_BUILD_CONFIGURATION=public-demo$/m);
  assert.match(dockerfile, /^RUN case "\$FRONTEND_BUILD_CONFIGURATION" in \\$/m);
  const allowed = [...dockerfile.matchAll(/^\s+(public-demo(?:-acceptance)?)\) npm run (build:public-demo(?:-acceptance)?) ;;/gm)]
    .map(([, value, script]) => [value, script]);
  assert.deepEqual(allowed, [
    ['public-demo', 'build:public-demo'],
    ['public-demo-acceptance', 'build:public-demo-acceptance'],
  ]);
  const arms = [...dockerfile.matchAll(/^\s+([^\s)]+)\) .*;;/gm)].map(([, value]) => value);
  assert.deepEqual(arms, ['public-demo', 'public-demo-acceptance', '*']);
  assert.match(dockerfile, /^\s+\*\) .*exit 2 ;;/m);
  assert.doesNotMatch(dockerfile, /npm run build:\$FRONTEND_BUILD_CONFIGURATION/);
});

test('Compose selects public-demo when the build variable is absent', () => {
  const compose = read('deploy/public-demo/compose.yaml');
  const example = read('deploy/public-demo/.env.example');
  assert.match(compose,
    /^\s+FRONTEND_BUILD_CONFIGURATION: \$\{PUBLIC_DEMO_FRONTEND_CONFIGURATION-public-demo\}$/m);
  assert.match(example, /^PUBLIC_DEMO_FRONTEND_CONFIGURATION=public-demo$/m);
});
