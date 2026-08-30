import { ChangeDetectionStrategy, Component } from '@angular/core';
import { CsCard, CsIcon, TranslatePipe } from 'common';

/**
 * Where roleGuard sends a signed-in user whose role cannot use a route. That
 * guard shipped with the frontend foundation already navigating here, so
 * without this route it navigated nowhere.
 */
@Component({
  selector: 'admin-forbidden',
  imports: [CsCard, CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './forbidden.component.html',
})
export default class ForbiddenComponent {}
