import { Component, input } from '@angular/core';
import { RunIteration } from '../run-models';

@Component({
  selector: 'app-history-panel',
  imports: [],
  templateUrl: './history-panel.html',
  styleUrl: './history-panel.css'
})
export class HistoryPanel {
  readonly history = input.required<readonly RunIteration[]>();
}
