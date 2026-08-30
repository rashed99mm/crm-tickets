import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CsIcon, TranslatePipe } from 'common';

@Component({
  selector: 'portal-solution',
  imports: [RouterLink, CsIcon, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './solution.component.html',
})
export default class PortalSolutionComponent {}
