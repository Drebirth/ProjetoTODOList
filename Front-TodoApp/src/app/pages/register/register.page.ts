import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthPageBase } from '../../core/auth-page.base';

@Component({ selector: 'app-register-page', imports: [FormsModule, RouterLink], templateUrl: './register.page.html' })
export class RegisterPage extends AuthPageBase {
  submit(): void { this.submitAuth(true); }
}
