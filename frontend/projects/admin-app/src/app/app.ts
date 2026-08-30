import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/**
 * The root component defers entirely to the router. `AdminShell` is not
 * rendered here — it is one of the routed components (`app.routes.ts`
 * mounts it on the authenticated parent route), so `/login` can render on
 * its own, without the nav bar and "Sign out" chrome that belong only to a
 * signed-in session.
 */
@Component({
  selector: 'admin-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
