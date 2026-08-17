import { Component, input } from '@angular/core';
import { GateResult } from '../run-models';

@Component({
  selector: 'app-gate-strip',
  imports: [],
  templateUrl: './gate-strip.html',
  styleUrl: './gate-strip.css'
})
export class GateStrip {
  readonly gates = input.required<readonly GateResult[]>();
}
