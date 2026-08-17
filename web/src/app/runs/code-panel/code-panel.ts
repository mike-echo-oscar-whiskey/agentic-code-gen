import { Component, input, signal } from '@angular/core';
import { CodeArtifact } from '../run-models';

@Component({
  selector: 'app-code-panel',
  imports: [],
  templateUrl: './code-panel.html',
  styleUrl: './code-panel.css'
})
export class CodePanel {
  readonly artifact = input<CodeArtifact | null>(null);
  readonly pending = input(false);

  protected readonly copied = signal(false);

  protected async copy(code: string): Promise<void> {
    await navigator.clipboard.writeText(code);
    this.copied.set(true);
    setTimeout(() => this.copied.set(false), 1500);
  }
}
