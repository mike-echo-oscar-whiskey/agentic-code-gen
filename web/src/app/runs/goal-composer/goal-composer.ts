import { Component, computed, input, output } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';

const MAX_GOAL_LENGTH = 500;

const EXAMPLE_GOALS = [
  'Search artworks by artist and return title, artist, date, department and image URL',
  'Fetch an object by id and format it as a one-line summary',
  'Filter search results down to objects that have a public-domain image'
] as const;

@Component({
  selector: 'app-goal-composer',
  imports: [ReactiveFormsModule],
  templateUrl: './goal-composer.html',
  styleUrl: './goal-composer.css'
})
export class GoalComposer {
  readonly busy = input(false);
  readonly start = output<string>();

  protected readonly maxLength = MAX_GOAL_LENGTH;
  protected readonly examples = EXAMPLE_GOALS;

  protected readonly goal = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(MAX_GOAL_LENGTH)]
  });

  private readonly value = toSignal(this.goal.valueChanges, {
    initialValue: this.goal.value
  });

  protected readonly characterCount = computed(() => this.value().length);
  protected readonly canSubmit = computed(
    () => this.value().trim().length > 0 && this.value().length <= MAX_GOAL_LENGTH
  );

  protected use(example: string): void {
    this.goal.setValue(example);
  }

  protected submit(): void {
    if (!this.canSubmit() || this.busy()) {
      return;
    }

    this.start.emit(this.value().trim());
  }
}
