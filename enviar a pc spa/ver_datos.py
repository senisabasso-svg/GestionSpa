#!/usr/bin/env python3
"""
Script para ver datos almacenados en la BD
"""
import sys
from database import Database
from config import Colors
import json

def print_header(title):
    print(f"\n{Colors.OKBLUE}{Colors.BOLD}{'═' * 60}{Colors.ENDC}")
    print(f"{Colors.OKBLUE}{Colors.BOLD}  {title}{Colors.ENDC}")
    print(f"{Colors.OKBLUE}{Colors.BOLD}{'═' * 60}{Colors.ENDC}\n")

def main():
    db = Database()

    print(f"""{Colors.HEADER}
╔═════════════════════════════════════════════════════════╗
║          📊 VISTA DE DATOS - BASE DE DATOS             ║
╚═════════════════════════════════════════════════════════╝
    """)

    # ESTADÍSTICAS
    print_header("📈 ESTADÍSTICAS GENERALES")
    stats = db.get_stats()
    print(f"{Colors.OKGREEN}✅ Total de usuarios: {stats['total_users']}{Colors.ENDC}")
    print(f"{Colors.OKGREEN}✅ Total de accesos: {stats['total_accesses']}{Colors.ENDC}")
    print(f"{Colors.OKGREEN}✅ Terminales en línea: {stats['online_terminals']}{Colors.ENDC}")
    print(f"{Colors.WARNING}⚠️ Alertas pendientes: {stats['pending_alerts']}{Colors.ENDC}")

    # ÚLTIMOS ACCESOS
    print_header("🚪 ÚLTIMOS ACCESOS (10)")
    logs = db.get_access_logs(10)

    if logs:
        for log in logs:
            conf = log['confidence']
            conf_txt = f"{conf*100:.1f}%" if conf is not None else "N/A"
            print(f"""
  {Colors.OKGREEN}✓{Colors.ENDC} {log['user_name']} ({log['user_id']})
    Terminal: {log['terminal_id']}
    Tipo: {log['access_type']}
    Método: {log['method']}
    Confianza: {conf_txt} | Temp: {log['temperature'] or 'N/A'}°C
    Hora: {log['created_at']}
            """)
    else:
        print(f"{Colors.WARNING}No hay registros de acceso{Colors.ENDC}")

    # USUARIOS
    print_header("👥 USUARIOS REGISTRADOS")
    users = db.get_connection().cursor()
    users.execute('SELECT * FROM users ORDER BY created_at DESC LIMIT 10')
    user_list = users.fetchall()

    if user_list:
        for user in user_list:
            print(f"""
  {Colors.OKBLUE}👤{Colors.ENDC} {user['first_name']} {user['last_name']}
    ID: {user['user_id']} | Email: {user['email']}
    Membresía: {user['membership_type']} | Nivel: {user['access_level']}
    Válido hasta: {user['valid_until']}
    Estado: {Colors.OKGREEN}{user['status'].upper()}{Colors.ENDC}
            """)
    else:
        print(f"{Colors.WARNING}No hay usuarios registrados{Colors.ENDC}")

    # TERMINALES
    print_header("🔌 TERMINALES")
    terminals = db.get_connection().cursor()
    terminals.execute('SELECT * FROM terminals ORDER BY updated_at DESC')
    terminal_list = terminals.fetchall()

    if terminal_list:
        for terminal in terminal_list:
            status_color = Colors.OKGREEN if terminal['status'] == 'online' else Colors.FAIL
            print(f"""
  Terminal ID: {terminal['terminal_id']}
    Status: {status_color}{terminal['status'].upper()}{Colors.ENDC}
    IP: {terminal['ip_address']}
    Último heartbeat: {terminal['last_heartbeat']}
            """)
    else:
        print(f"{Colors.WARNING}No hay terminales registradas{Colors.ENDC}")

    # ALERTAS
    print_header("⚠️ ALERTAS RECIENTES")
    alerts = db.get_connection().cursor()
    alerts.execute('SELECT * FROM alerts WHERE resolved = 0 ORDER BY created_at DESC LIMIT 5')
    alert_list = alerts.fetchall()

    if alert_list:
        for alert in alert_list:
            severity_color = Colors.FAIL if alert['severity'] == 'critical' else Colors.WARNING
            print(f"""
  {severity_color}[{alert['severity'].upper()}]{Colors.ENDC} {alert['alert_type']}
    Terminal: {alert['terminal_id']}
    Mensaje: {alert['message']}
    Hora: {alert['created_at']}
            """)
    else:
        print(f"{Colors.OKGREEN}✅ No hay alertas pendientes{Colors.ENDC}")

    print(f"\n{Colors.OKBLUE}{'═' * 60}{Colors.ENDC}\n")


if __name__ == '__main__':
    try:
        main()
    except KeyboardInterrupt:
        print(f"\n{Colors.WARNING}Operación cancelada{Colors.ENDC}")
        sys.exit(0)
    except Exception as e:
        print(f"{Colors.FAIL}Error: {e}{Colors.ENDC}")
        sys.exit(1)
