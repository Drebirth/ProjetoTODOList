import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthPageBase } from '../../core/auth-page.base';

@Component({ selector: 'app-login-page', imports: [FormsModule, RouterLink], templateUrl: './login.page.html' })
export class LoginPage extends AuthPageBase {
  submit(): void { this.submitAuth(false); }
}
